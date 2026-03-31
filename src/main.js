const { invoke } = window.__TAURI__.core;
const { listen } = window.__TAURI__.event;

window.addEventListener('dragover', (e) => e.preventDefault());
window.addEventListener('drop', (e) => e.preventDefault());

const dropZone = document.getElementById('drop-zone');

let selectedMetadataPath = '';

listen('tauri://drag-drop', (event) => {
    const paths = event.payload.paths;
    
    if (paths && paths.length > 0) {
        const filePath = paths[0];

        const uploadPage = document.getElementById('page-upload');
        const downloadPage = document.getElementById('page-download');

        if (uploadPage && !uploadPage.classList.contains('hidden')) {
            document.getElementById('upload-path').value = filePath;
            document.getElementById('btn-upload').disabled = false;
            flashInput('upload-path');
        } 

        else if (downloadPage && !downloadPage.classList.contains('hidden')) {
            if (filePath.endsWith('.json')) {
                
                window.selectedMetadataPath = filePath; 
                document.getElementById('input-metadata-path').value = filePath;
                flashInput('input-metadata-path');
                
                if (typeof window.setDlMethod === 'function') {
                    window.setDlMethod('file');
                }
            }
        }
    }
    
    const dz = document.getElementById('drop-zone');
    if (dz) dz.classList.remove('border-indigo-500', 'bg-indigo-500/10', 'scale-[1.01]');
});

listen('tauri://drag-over', () => {
    const dz = document.getElementById('drop-zone');
    const uploadPage = document.getElementById('page-upload');
    
    if (dz && uploadPage && !uploadPage.classList.contains('hidden')) {
        dz.classList.add('border-indigo-500', 'bg-indigo-500/10', 'scale-[1.01]');
    }
});

listen('tauri://drag-leave', () => {
    const dz = document.getElementById('drop-zone');
    if (dz) dz.classList.remove('border-indigo-500', 'bg-indigo-500/10', 'scale-[1.01]');
});

function flashInput(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.add('ring-2', 'ring-indigo-500', 'border-indigo-500');
    setTimeout(() => el.classList.remove('ring-2', 'ring-indigo-500', 'border-indigo-500'), 1500);
}

async function updateAuthUI() {
    const statusEl = document.getElementById('discord-status');
    const loginBtn = document.getElementById('btn-login');
    const logoffBtn = document.getElementById('btn-logoff');

    try {
        const isAuthenticated = await invoke('check_discord_auth');
        
        if (isAuthenticated) {
            statusEl.textContent = "Authenticated";
            statusEl.className = "text-sm text-green-400 font-medium transition-colors duration-300";
            loginBtn.classList.add('hidden');
            logoffBtn.classList.remove('hidden');
        } else {
            statusEl.textContent = "Not authenticated";
            statusEl.className = "text-sm text-gray-500 transition-colors duration-300";
            loginBtn.classList.remove('hidden');
            logoffBtn.classList.add('hidden');
        }
    } catch (e) {
        console.error("Auth check failed:", e);
        statusEl.textContent = "Authentication Error";
        statusEl.className = "text-sm text-red-400 transition-colors duration-300";
        // Show login button as fallback
        loginBtn.classList.remove('hidden');
    }
}


window.logOff = async function() {
    try {
        await invoke('log_off');
        await updateAuthUI();
        window.PopupAPI && window.PopupAPI.show('Session', 'Logged off successfully!');
        setTimeout(() => window.PopupAPI && window.PopupAPI.hide(), 1500);
    } catch (e) {
        console.error("Logoff failed:", e);
    }
}

window.loadSettings = async function() {
    try {
        const settings = await invoke('get_settings');
        document.getElementById('settings-trash-channel').value = settings.trash_channel || '';
        document.getElementById('settings-chunk-size').value = Math.floor((settings.max_file_size || (8*1024*1024))/1024/1024);
        
        await updateAuthUI(); 
    } catch (e) {
        console.error('Failed to load settings:', e);
    }
}

async function checkAuth(token) {
    const statusEl = document.getElementById('discord-status');
    const loginBtn = document.getElementById('btn-login');
    const logoffBtn = document.getElementById('btn-logoff');

    if (!token) {
        statusEl.textContent = "Not authenticated";
        statusEl.className = "text-sm text-gray-500 transition-colors duration-300";
        loginBtn.classList.remove('hidden');
        logoffBtn.classList.add('hidden');
        return;
    }

    try {
        const isValid = await invoke('check_discord_auth', { token: token });
        
        if (isValid) {
            statusEl.textContent = "Authenticated";
            statusEl.className = "text-sm text-green-400 font-medium transition-colors duration-300";
            loginBtn.classList.add('hidden');
            logoffBtn.classList.remove('hidden');
        } else {
            statusEl.textContent = "Token Invalid - Please login again";
            statusEl.className = "text-sm text-red-400 font-medium transition-colors duration-300";
            loginBtn.classList.remove('hidden');
            logoffBtn.classList.add('hidden');
        }
    } catch (e) {
        console.error("Auth check failed:", e);
        statusEl.textContent = "Error checking auth";
        statusEl.className = "text-sm text-orange-400 transition-colors duration-300";
    }
}

async function saveSettings() {
    try {
        const trashChannel = document.getElementById('settings-trash-channel').value;
        const chunkSizeMB = parseInt(document.getElementById('settings-chunk-size').value, 10);
        const settings = await invoke('get_settings');
        settings.trash_channel = trashChannel;
        settings.max_file_size = chunkSizeMB * 1024 * 1024;
        await invoke('save_settings', { settings });
        window.PopupAPI && window.PopupAPI.show('Settings', 'Settings saved!');
        setTimeout(() => window.PopupAPI && window.PopupAPI.hide(), 1200);
    } catch (e) {
        console.error('Failed to save settings:', e);
        window.PopupAPI && window.PopupAPI.show('Settings', 'Failed to save!');
        setTimeout(() => window.PopupAPI && window.PopupAPI.hide(), 2000);
    }
}

document.getElementById('btn-save-settings').addEventListener('click', saveSettings);


async function openFolder(file, download = false) {
    const folderPath = await invoke('select_directory', { file: file });
    if (folderPath) {
        if (download) {
            if (file) {
                document.getElementById('input-metadata-path').value = folderPath;
                selectedMetadataPath = folderPath;
            }else{
                document.getElementById('input-dest-path').value = folderPath;
                document.getElementById('btn-download').disabled = false;
            }
        } else {
            document.getElementById('upload-path').value = folderPath;
            document.getElementById('btn-upload').disabled = false;
        }
    }
}

async function uploadFile(file, mbChunks) {
    let unlisten;

    try {
        window.PopupAPI.show("Upload", "Preparing file...");

        unlisten = await listen('upload-progress', (event) => {
            const payload = event.payload;
            window.PopupAPI.update(Math.round(payload.progress), payload.message);
        });

        await invoke('uploadFile', { file: file, size: mbChunks, finalChannel: document.getElementById("metadata-channel").value, trashChannel: document.getElementById("settings-trash-channel").value });

        window.PopupAPI.update(100, "Upload Complete!");
        setTimeout(() => window.PopupAPI.hide(), 1500);

    } catch (err) {
        console.error(err);
        window.PopupAPI.update(0, "Error: " + err);
        setTimeout(() => window.PopupAPI.hide(), 4000);
    } finally {
        if (unlisten) {
            unlisten();
        }
    }
}

window.fetchDiscordToken = async function() {
    try {
        await invoke('fetch_discord_token');
        
        await updateAuthUI(); 
        
        console.log("Login réussi et UI mise à jour");
    } catch (e) {
        console.error("Erreur lors du login:", e);
        await updateAuthUI();
    }
}

document.getElementById('btn-download').addEventListener('click', async () => {
    const btn = document.getElementById('btn-download');
    const destinationPath = document.getElementById('input-dest-path').value;
    let manifestJson = "";
    let unlisten;

    try {
        if (window.currentDownloadMethod === 'text') {
            manifestJson = document.getElementById('input-dl-id').value;
        } else {
            if (!selectedMetadataPath) throw "Please select a .json metadata file";
            manifestJson = await invoke('read_text_file', {path: selectedMetadataPath});
        }

        if (!destinationPath) throw "Please choose a destination folder";
        if (!manifestJson) throw "Invalid ID or Manifest.. " + manifestJson;

        btn.disabled = true;
        btn.innerText = "Downloading...";
        window.PopupAPI.show("Download", "Connecting to Discord...");

        unlisten = await listen('download-progress', (event) => {
            const payload = event.payload;
            window.PopupAPI.update(payload.progress, payload.message);
        });

        const result = await invoke("download_everything", {
            manifestJson: manifestJson,
            exportPath: destinationPath
        });

        window.PopupAPI.update(100, "Download Complete!");
        setTimeout(() => window.PopupAPI.hide(), 1500);

    } catch (err) {
        console.error(err);
        window.PopupAPI.update(0, "Error: " + err);
        setTimeout(() => window.PopupAPI.hide(), 4000);
    } finally {
        btn.disabled = false;
        btn.innerText = "Start Download";
        if (unlisten) {
            unlisten(); 
        }
    }
});

window.openFolder = openFolder;
window.uploadFile = uploadFile;
window.fetchDiscordToken = fetchDiscordToken;