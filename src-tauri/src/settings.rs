use tauri::Manager;
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::PathBuf;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Settings {
    pub discord_token: String,
    pub max_file_size: u64,
    pub trash_channel: String,
    pub auto_compress: bool,
    pub output_dir: String,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            discord_token: String::new(),
            max_file_size: 9 * 1024 * 1024,
            trash_channel: "123456789".to_string(),
            auto_compress: true,
            output_dir: String::new(),
        }
    }
}

use tauri::AppHandle;
#[tauri::command]
pub fn get_settings(app: AppHandle) -> Result<Settings, String> {
    let config_dir = app.path().app_local_data_dir().map_err(|e| e.to_string())?;
    let manager = SettingsManager::new(config_dir.to_str().unwrap());
    Ok(manager.load_or_default())
}

#[tauri::command]
pub fn save_settings(app: AppHandle, settings: Settings) -> Result<(), String> {
    let config_dir = app.path().app_local_data_dir().map_err(|e| e.to_string())?;
    let manager = SettingsManager::new(config_dir.to_str().unwrap());
    manager.save(&settings).map_err(|e| e.to_string())
}

pub struct SettingsManager {
    config_path: PathBuf,
}

impl SettingsManager {
    pub fn new(config_dir: &str) -> Self {
        let path = PathBuf::from(config_dir).join("settings.json");
        Self { config_path: path }
    }

    pub fn load(&self) -> Result<Settings, Box<dyn std::error::Error>> {
        let data = fs::read_to_string(&self.config_path)?;
        Ok(serde_json::from_str(&data)?)
    }

    pub fn load_or_default(&self) -> Settings {
        self.load().unwrap_or_default()
    }

    pub fn save(&self, settings: &Settings) -> Result<(), Box<dyn std::error::Error>> {
        fs::create_dir_all(self.config_path.parent().unwrap())?;
        let data = serde_json::to_string_pretty(settings)?;
        fs::write(&self.config_path, data)?;
        Ok(())
    }
}