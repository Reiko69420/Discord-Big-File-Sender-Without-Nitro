mod settings;
// Learn more about Tauri commands at https://tauri.app/develop/calling-rust/
use std::io::{self, Read, Write, BufReader, Cursor};
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::fs;
use reqwest::header::{HeaderMap, HeaderValue, AUTHORIZATION, CONTENT_TYPE, USER_AGENT};
use tauri::{AppHandle, Listener, WebviewUrl, WebviewWindowBuilder};
use tauri::Manager;
use tauri_plugin_dialog::FileDialogBuilder;
use tauri_plugin_dialog::DialogExt;
use std::fs::{File};
use flate2::write::ZlibEncoder;
use flate2::Compression;
use tar::Builder;
use url::Url;
use serde_json::Value;
use flate2::read::ZlibDecoder;
use tokio::fs as async_fs;
use tokio::io::AsyncWriteExt;
use tauri::Emitter;


#[derive(Clone, Serialize)]
struct ProgressPayload {
    progress: f64,
    message: String,
}

#[derive(Serialize, Deserialize)]
struct DiscordAttachment {
    id: String,
    filename: String,
    upload_url: Option<String>,
    upload_filename: Option<String>,
}

#[derive(Serialize)]
struct ManifestItem {
    i: String,
    e: String,
}

struct DiscordClient {
    client: reqwest::Client,
    token: String,
}

struct PartWriter {
    temp_dir: PathBuf,
    base_name: String,
    part_limit: u64,
    current_part_size: u64,
    part_counter: u32,
    current_file: File,
}

impl PartWriter {
    fn new(temp_dir: PathBuf, base_name: String, part_limit: u64) -> io::Result<Self> {
        // Zero-padding {:03} ensures correct alphabetical sorting (part001, part002)
        let first_path = temp_dir.join(format!("{}.part{:03}", base_name, 1));
        let file = File::create(first_path)?;
        Ok(Self {
            temp_dir,
            base_name,
            part_limit,
            current_part_size: 0,
            part_counter: 1,
            current_file: file,
        })
    }
}

impl Write for PartWriter {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        if self.current_part_size + (buf.len() as u64) > self.part_limit {
            self.part_counter += 1;
            let next_path = self.temp_dir.join(format!("{}.part{:03}", self.base_name, self.part_counter));
            self.current_file = File::create(next_path)?;
            self.current_part_size = 0;
        }

        self.current_file.write_all(buf)?;
        self.current_part_size += buf.len() as u64;
        Ok(buf.len())
    }

    fn flush(&mut self) -> io::Result<()> {
        self.current_file.flush()
    }
}

impl DiscordClient {
    fn new(token: &str) -> Self {
        let mut headers = HeaderMap::new();
        headers.insert(USER_AGENT, HeaderValue::from_static("Discord-Android/172024"));
        headers.insert(AUTHORIZATION, HeaderValue::from_str(token).unwrap());
        
        Self {
            client: reqwest::Client::builder()
                .default_headers(headers)
                .build()
                .unwrap(),
            token: token.to_string(),
        }
    }

    async fn upload_file(&self, channel_id: &str, file_path: &Path, content_type: &str) -> Result<i64, String> {
        let file_name = file_path.file_name().unwrap().to_str().unwrap();
        let file_data = fs::read(file_path).await.map_err(|e| e.to_string())?;
        let file_size = file_data.len();

        let pre_upload_body = json!({
            "files": [{ "filename": file_name, "file_size": file_size, "id": "0" }]
        });

        let res = self.client.post(format!("https://discord.com/api/v9/channels/{}/attachments", channel_id))
            .json(&pre_upload_body)
            .send().await.map_err(|e| e.to_string())?;

        let json_res: serde_json::Value = res.json().await.map_err(|e| e.to_string())?;
        let upload_url = json_res["attachments"][0]["upload_url"].as_str().ok_or("No upload URL")?;
        let discord_fn = json_res["attachments"][0]["upload_filename"].as_str().ok_or("No discord filename")?;

        self.client.put(upload_url)
            .header(CONTENT_TYPE, content_type)
            .body(file_data)
            .send().await.map_err(|e| e.to_string())?;

        let final_body = json!({
            "content": "",
            "channel_id": channel_id,
            "attachments": [{ "id": "0", "filename": file_name, "uploaded_filename": discord_fn }]
        });

        let res_final = self.client.post(format!("https://discord.com/api/v9/channels/{}/messages", channel_id))
        .json(&final_body)
        .send().await.map_err(|e| e.to_string())?;

        let msg_res: serde_json::Value = res_final.json().await.map_err(|e| e.to_string())?;
        let message_id = msg_res["id"].as_str().ok_or("No Message ID found")?;
        
        message_id.parse::<i64>().map_err(|e| e.to_string())
    }
}

#[tauri::command]
fn read_text_file(path: String) -> Result<String, String> {
    std::fs::read_to_string(path).map_err(|e| e.to_string())
}

fn retrieve_token(app: &AppHandle) -> Result<String, String> {
    let settings = crate::settings::get_settings(app.clone())?;
    let token = settings.discord_token;
    if token.is_empty() {
        Err("Token is empty! Please log in again.".to_string())
    } else {
        Ok(token)
    }
}

async fn upload_everything(
    app: tauri::AppHandle, 
    token: String, 
    trash_channel: String, 
    final_channel: String,
    temp_path: PathBuf
) -> Result<(), String> {
    let discord = Arc::new(DiscordClient::new(&token));
    let mut base_id: i64 = 0;
    let mut parts_data = Vec::new(); 
    let mut last_name = String::new();

    let mut entries = tokio::fs::read_dir(&temp_path).await.map_err(|e| e.to_string())?;
    let mut file_paths = Vec::new();

    while let Some(entry) = entries.next_entry().await.map_err(|e| e.to_string())? {
        let path = entry.path();
        if path.is_file() {
            file_paths.push(path);
        }
    }

    // Sort alphabetically to ensure parts are sent in the correct order
    file_paths.sort();
    let total_parts = file_paths.len() as f64;

    for (index, path) in file_paths.iter().enumerate() {
        last_name = path.file_stem().unwrap().to_str().unwrap().to_string();

        // Calculate upload progress (Starts at 40%, goes up to 95%)
        let current_progress = 40.0 + (((index as f64) / total_parts) * 55.0);
        let _ = app.emit("upload-progress", ProgressPayload {
            progress: current_progress,
            message: format!("Uploading chunk {}/{}...", index + 1, file_paths.len()),
        });
        tokio::task::yield_now().await;

        let current_id = discord.upload_file(&trash_channel, path, "application/x-zip-compressed").await?;
        
        parts_data.push((current_id - base_id).to_string());
        base_id = current_id;
        
        tokio::time::sleep(std::time::Duration::from_millis(250)).await;
    }

    let _ = app.emit("upload-progress", ProgressPayload { progress: 96.0, message: "Uploading manifest...".to_string() });
    tokio::task::yield_now().await;

    let manifest_json = json!({
        "c": trash_channel,
        "n": last_name,
        "p": parts_data 
    });

    let app_dir = app.path().app_cache_dir().unwrap();
    let manifest_path = app_dir.join(format!("{}.txt", last_name));
    
    tokio::fs::write(&manifest_path, manifest_json.to_string()).await.map_err(|e| e.to_string())?;

    discord.upload_file(&final_channel, &manifest_path, "text/plain").await?;

    let _ = app.emit("upload-progress", ProgressPayload { progress: 98.0, message: "Cleaning up...".to_string() });
    
    let _ = tokio::fs::remove_dir_all(&temp_path).await;
    let _ = tokio::fs::remove_file(&manifest_path).await;

    Ok(())
}

async fn compress_to_parts(
    app: tauri::AppHandle, 
    source_path: &str, 
    part_size_mb: u64
) -> Result<String, String> {
    let source = Path::new(&source_path);
    let temp_dir = app.path().app_cache_dir().map_err(|e| e.to_string())?.join("discord_parts");
    
    if temp_dir.exists() { 
        let _ = tokio::fs::remove_dir_all(&temp_dir).await; 
    }
    tokio::fs::create_dir_all(&temp_dir).await.map_err(|e| e.to_string())?;

    let file_name = source.file_name().unwrap().to_string_lossy().into_owned();
    let part_limit = part_size_mb * 1024 * 1024;

    let smart_writer = PartWriter::new(temp_dir.clone(), file_name.clone(), part_limit)
        .map_err(|e| e.to_string())?;

    let mut encoder = ZlibEncoder::new(smart_writer, Compression::fast());

    // We track the file size to calculate progress accurately
    let file_meta = std::fs::metadata(source).map_err(|e| e.to_string())?;
    let total_size = file_meta.len() as f64;
    let mut processed_size = 0f64;

    if source.is_dir() {
        let _ = app.emit("upload-progress", ProgressPayload { progress: 10.0, message: "Compressing directory...".to_string() });
        let mut tar_builder = tar::Builder::new(&mut encoder);
        tar_builder.append_dir_all(".", source).map_err(|e| e.to_string())?;
        tar_builder.finish().map_err(|e| e.to_string())?;
    } else {
        let mut reader = BufReader::new(File::open(source).map_err(|e| e.to_string())?);
        let mut buffer = vec![0; 1024 * 1024]; // 1MB buffer
        loop {
            let n = reader.read(&mut buffer).map_err(|e| e.to_string())?;
            if n == 0 { break; }
            encoder.write_all(&buffer[..n]).map_err(|e| e.to_string())?;
            
            processed_size += n as f64;
            let current_progress = (processed_size / total_size) * 40.0;
            
            let _ = app.emit("upload-progress", ProgressPayload {
                progress: current_progress,
                message: format!("Compressing data... {:.1}%", (processed_size / total_size) * 100.0),
            });
            
            tokio::task::yield_now().await;
        }
    }

    encoder.finish().map_err(|e| e.to_string())?;
    Ok(temp_dir.to_string_lossy().to_string())
}

#[tauri::command]
async fn uploadFile(app: tauri::AppHandle, file: String, size: u64, final_channel: String, trash_channel: String) -> Result<String, String> {
    let _ = app.emit("upload-progress", ProgressPayload {
        progress: 0.0,
        message: "Authenticating...".to_string(),
    });

    let token = retrieve_token(&app)?;

    if token.is_empty() {
        return Err("Token is empty! Please log in again.".to_string());
    }

    let temp_dir_str = compress_to_parts(app.clone(), &file, size).await?;
    let temp_dir = PathBuf::from(temp_dir_str);

    upload_everything(app.clone(), token, trash_channel, final_channel, temp_dir).await?;

    Ok("Upload completed".to_string())
}

#[tauri::command]
async fn select_directory(app: tauri::AppHandle, file: bool) -> Option<String> {
    let (tx, rx) = std::sync::mpsc::channel();

    if file {
        app.dialog().file().pick_file(move |path_buf| {
            let path_str = path_buf.map(|p| p.to_string());
            tx.send(path_str).unwrap(); 
        });
    } else {  
        app.dialog().file().pick_folder(move |path_buf| {
            let path_str = path_buf.map(|p| p.to_string());
            tx.send(path_str).unwrap(); 
        });
    }

    rx.recv().unwrap()
}

#[tauri::command]
async fn fetch_discord_token(app: AppHandle) -> Result<(), String> {
    tauri::async_runtime::spawn(async move {
        let js_injection = r#"
            const checkInterval = setInterval(() => {
                if (window.location.href.includes("/channels/")) {
                    try {
                        const iframe = document.createElement('iframe');
                        iframe.style.display = 'none';
                        document.body.appendChild(iframe);
                        
                        let rawToken = iframe.contentWindow.localStorage.token;
                        
                        document.body.removeChild(iframe);

                        if (rawToken) {
                            clearInterval(checkInterval);
                            let cleanToken = JSON.parse(rawToken);
                            
                            window.location.href = "https://auth.done/?token=" + encodeURIComponent(cleanToken);
                        }
                    } catch (e) {

                    }
                }
            }, 1000);
        "#;

        let login_window = WebviewWindowBuilder::new(
            &app,
            "discord_login",
            WebviewUrl::External("https://discord.com/login".parse().unwrap())
        )
        .title("Connexion Discord")
        .user_agent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36")
        .initialization_script(js_injection)
        .inner_size(900.0, 600.0)
        .on_navigation({
            let app_handle = app.clone();
            move |url| {
                if url.host_str() == Some("auth.done") {
                    let token_pair = url.query_pairs().find(|(key, _)| key == "token");
                    
                    if let Some((_, value)) = token_pair {
                        let final_token = value.to_string();
                        
                        if let Ok(mut settings) = crate::settings::get_settings(app_handle.clone()) {
                            settings.discord_token = final_token;
                            let _ = crate::settings::save_settings(app_handle.clone(), settings);
                            
                        }

                        // Retrieve the window by its label and close it
                        if let Some(window) = app_handle.get_webview_window("discord_login") {
                            let _ = window.close();
                        }
                    }
                    return false; 
                }
                true
            }
        })
        .build()
        .expect("Cannot create the login window... Swad");

        let window_clone = login_window.clone();

    });

    Ok(())
}

#[tauri::command]
async fn check_discord_auth(app: AppHandle) -> Result<bool, String> {
    let token = retrieve_token(&app)?;
    
    if token.is_empty() { return Ok(false); }

    let client = reqwest::Client::new();
    let res = client.get("https://discord.com/api/v9/users/@me")
        .header("Authorization", &token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    Ok(res.status().is_success())
}

#[tauri::command]
async fn log_off(app: AppHandle) -> Result<(), String> {
    let mut settings = crate::settings::get_settings(app.clone())?;
    settings.discord_token = "".to_string();
    crate::settings::save_settings(app.clone(), settings)?;

    let js_clear = r#"
        window.localStorage.clear();
        window.sessionStorage.clear();
        document.cookie.split(";").forEach(function(c) { 
            document.cookie = c.replace(/^ +/, "").replace(/=.*/, "=;expires=" + new Date().toUTCString() + ";path=/;domain=.discord.com"); 
        });
        window.location.href = "https://auth.done/logout";
    "#;

    let _ = WebviewWindowBuilder::new(
        &app,
        "discord_logout",
        WebviewUrl::External("https://discord.com/login".parse().unwrap())
    )
    .visible(false)
    .initialization_script(js_clear)
    .on_navigation({
        let app_handle = app.clone();
        move |url| {
            if url.host_str() == Some("auth.done") {
                if let Some(window) = app_handle.get_webview_window("discord_logout") {
                    let _ = window.close();
                }
                return false;
            }
            true
        }
    })
    .build()
    .map_err(|e| e.to_string())?;

    Ok(())
}

#[tauri::command]
async fn download_everything(
    app: tauri::AppHandle, 
    manifest_json: String, 
    export_path: String
) -> Result<String, String> {
    let emit_progress = |progress: f64, message: &str| {
        let payload = ProgressPayload {
            progress,
            message: message.to_string(),
        };
        let _ = app.emit("download-progress", payload); 
    };

    emit_progress(0.0, "Connecting to Discord...");

    let token = retrieve_token(&app)?;
    
    if token.is_empty() { 
        return Err("Token is empty! Please log in again.".to_string()); 
    }

    let manifest: serde_json::Value = serde_json::from_str(&manifest_json)
        .map_err(|e| format!("JSON parsing error: {}", e))?;

    let mut old_id: i64 = 0;
    let channel_id = manifest["c"].as_str().ok_or("Missing 'c' key")?;
    let original_filename = manifest["n"].as_str().unwrap_or("restored_file");
    let parts = manifest["p"].as_array().ok_or("'p' key is not an array")?;
    let total_parts = parts.len() as f64;

    let export_dir = PathBuf::from(&export_path);
    let temp_dir = export_dir.join("temp_download_dbfu");
    
    if temp_dir.exists() {
        let _ = async_fs::remove_dir_all(&temp_dir).await;
    }
    async_fs::create_dir_all(&temp_dir).await.map_err(|e| e.to_string())?;
    
    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(30))
        .build()
        .map_err(|e| format!("Client creation error: {}", e))?;

    let mut downloaded_files: Vec<PathBuf> = Vec::new();

    const SUPER_PROPERTIES: &str = "eyJvcyI6IkFuZHJvaWQiLCJicm93c2VyIjoiRGlzY29yZCBBbmRyb2lkIiwiZGV2aWNlIjoiU00tRzk5MUIiLCJzeXN0ZW1fbG9jYWxlIjoiZW4tVVMiLCJjbGllbnRfdmVyc2lvbiI6IjE3Mi4yNCAtIDE3MjAyNCIsInJlbGVhc2VfY2hhbm5lbCI6Imdvb2dsZVBsYXkiLCJvc192ZXJzaW9uIjoiMzMiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjoxNzIwMjR9";

    for (index, part_offset) in parts.iter().enumerate() {
        let offset_str = part_offset.as_str().ok_or("Invalid part format")?;
        let offset: i64 = offset_str.parse().map_err(|e: std::num::ParseIntError| e.to_string())?;
        let message_id = old_id + offset;
        old_id = message_id;

        // Emit real-time progress
        let current_progress = ((index as f64) / total_parts) * 80.0;
        emit_progress(current_progress, &format!("Fetching fragment {}/{}...", index + 1, parts.len()));

        let msg_url = format!(
            "https://discord.com/api/v9/channels/{}/messages?limit=1&around={}", 
            channel_id, 
            message_id
        );
        
        let msg_res = client.get(&msg_url)
            .header("Authorization", &token)
            .header("User-Agent", "Discord-Android/172024")
            .header("X-Super-Properties", SUPER_PROPERTIES)
            .send().await.map_err(|e| format!("Network error: {}", e))?;

        if !msg_res.status().is_success() {
            return Err(format!("Discord error: {}", msg_res.status()));
        }

        let msg_list: Vec<serde_json::Value> = msg_res.json().await.map_err(|e| e.to_string())?;
        let msg_data = msg_list.get(0).ok_or("Message not found")?;
        
        let attachment = &msg_data["attachments"][0];
        let attachment_url = attachment["url"].as_str().ok_or("URL not found")?;
        let filename = attachment["filename"].as_str().unwrap_or("part.bin");

        println!("Downloading part {}: {}", index, filename);

        let file_res = client.get(attachment_url).send().await.map_err(|e| e.to_string())?;
        let file_bytes = file_res.bytes().await.map_err(|e| e.to_string())?;

        let file_path = temp_dir.join(filename);
        
        async_fs::write(&file_path, &file_bytes).await.map_err(|e| e.to_string())?;
        
        downloaded_files.push(file_path);

        tokio::task::yield_now().await;
        tokio::time::sleep(std::time::Duration::from_millis(250)).await;
    }

    emit_progress(80.0, "Sorting fragments...");

    downloaded_files.sort();

    let compressed_merged_path = temp_dir.join("merged_compressed.zlib");
    let mut merged_file = async_fs::File::create(&compressed_merged_path).await.map_err(|e| e.to_string())?;

    emit_progress(85.0, "Merging files together...");
    tokio::task::yield_now().await;

    for file_path in downloaded_files {
        let name = file_path.file_name().and_then(|n| n.to_str()).unwrap_or("part");
        println!("Merging: {}", name);
        
        // ASYNC READ & WRITE
        let part_data = async_fs::read(&file_path).await.map_err(|e| e.to_string())?;
        merged_file.write_all(&part_data).await.map_err(|e| e.to_string())?;
    }
    
    merged_file.sync_all().await.map_err(|e| e.to_string())?;

    emit_progress(90.0, "Decompressing and Restoring...");
    tokio::task::yield_now().await;

    let compressed_in = std::fs::File::open(&compressed_merged_path).map_err(|e| e.to_string())?;
    let mut decoder = ZlibDecoder::new(compressed_in);

    let mut header = [0u8; 512];
    let bytes_read = std::io::Read::read(&mut decoder, &mut header).map_err(|e| e.to_string())?;
    
    let is_tar = bytes_read >= 262 && &header[257..262] == b"ustar";

    let mut full_stream = std::io::Cursor::new(&header[..bytes_read]).chain(decoder);

    let final_dest_path = export_dir.join(&original_filename);

    if is_tar {
        emit_progress(95.0, "Extracting folder on-the-fly...");
        let mut archive = tar::Archive::new(full_stream);
        
        std::fs::create_dir_all(&final_dest_path).map_err(|e| e.to_string())?;
        archive.unpack(&final_dest_path).map_err(|e| format!("Extraction failed: {}", e))?;
    } else {
        emit_progress(95.0, "Writing file on-the-fly...");
        let mut final_out = std::fs::File::create(&final_dest_path).map_err(|e| e.to_string())?;
        std::io::copy(&mut full_stream, &mut final_out).map_err(|e| e.to_string())?;
    }

    // 6. Cleanup (Async)
    emit_progress(98.0, "Cleaning up temporary files...");
    tokio::task::yield_now().await;
    
    let _ = async_fs::remove_dir_all(&temp_dir).await;

    emit_progress(100.0, "Done!");
    Ok(format!("Successfully restored: {}", original_filename))
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            select_directory,
            uploadFile,
            fetch_discord_token,
            download_everything,
            check_discord_auth,
            log_off,
            read_text_file,
            crate::settings::get_settings,
            crate::settings::save_settings,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
