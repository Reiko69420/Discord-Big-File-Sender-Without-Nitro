# 📦 Discord GIGANTIC File Sender (Without nitro!)

Ever wanted to send a massive folder (Like a game, an app) or a huge video to a friend over Discord, only to be slapped with that annoying file size limit "10mb" or "500mb"?

Well here you got this app! Built with Rust and Tauri that bypasses this limit by chopping your files into Discord-friendly chunks, putting them in a "Trash Channel" and giving you a simple manifest to stitch them all back together!

No subscriptions, no third-party cloud drives, just pure chunking magic~

<img width="1489" height="1188" alt="image" src="https://github.com/user-attachments/assets/f6333415-cf16-4208-ac40-c203db47df4d" />

---

## ⚠️ The ""Don't Sue Me"" Disclaimer (Read This!)

**This application violates Discord's Terms of Service.** Using Discord as a personal file-hosting CDN is frowned upon by the platform. 

Could you get banned for this? **Technically, yes.** Are the chances of actually getting banned extremely slim? **Also yes, let's be honest.** Still, **I highly recommend using a secondary/burner Discord account** to log in and upload files through this app. I am **NOT** responsible if your account gets nuked, shadowbanned, or sent to the shadow realm. You have been warned!

---

## 🛠️ How It Works (Under the Hood)

Discord limits file sizes, but they don't limit how many messages you can send! Here is the nitty-gritty of how this app cheats the system:

1. **The Login:** The app opens a window to the Discord login screen where you just scan the QR Code or just log in to your acc normaly, once you log in, it grabs your session token and saves it locally in the app! This allows the app to send all the files and gather them on discord!
2. **Compression & Slicing:** When you drop a file or folder into the app it uses `tar` and `zlib` to compress it into a single archive, then, it aggressively chops that archive into pieces (default is 9MB per chunk)
3. **The "Trash Channel" Upload:** The app spams all these chunks into a specific Discord channel of your choosing (your "Trash Channel") with a small delay to not get rate limited
4. **The Manifest:** Once every chunks uploaded it will creates a `.json` manifest file, this is essentially a treasure map containing the IDs of all the messages holding your file chunks, so the channel ID and all the messages IDs, to make the manifest smaller i just make an ""offset"" for each ID like this: ActualId - PreviousId, this allow for smaller manifest
5. **Downloading & Stitching:** When someone wants to download the file, they load the manifest into the app, (**Because Discord attachment links expire after 24hrs**) the app uses the message IDs to fetch the *fresh, active* CDN links directly from the channel, it downloads all the chunks, stitches them together, decompresses them, and boom—you have your original file!

> **🚨 CRITICAL:** Anyone who wants to download the file **MUST** be in the server/group chat or wathever and have read access to the same "Trash Channel". If they can't see the messages, the app can't generate the fresh download links!

---

## ⚙️ How to Compile

This project is built using pure **Cargo/Rust** for the backend and **Vanilla JS + Tailwind CSS (via CDN)** for the frontend.

## Prerequisites
1.  Install [Rust](https://www.rust-lang.org/tools/install).
2.  Install the necessary system dependencies for Tauri (Check the [Tauri Prerequisites Guide](https://v2.tauri.app/start/prerequisites/) for your specific OS).
3.  Install the Tauri CLI via Cargo:
    `cargo install tauri-cli`

### Building the App
Clone the repository, open your terminal in the project folder, and run:

**To run it in development mode (hot-reloading):**
`cargo tauri dev`

**To compile a final, optimized application for your OS:**
`cargo tauri build`

The compiled executable will be waiting for you in `src-tauri/target/release/`.

---

## 🚀 Setup & Usage

1.  **Log In:** Open the app and go to settings then click the Login button. Log into your Discord account (again, a burner account is recommended) <img width="1492" height="1194" alt="image" src="https://github.com/user-attachments/assets/6a1adaee-3a7e-42bb-8631-058b48dd211c" />
2.  **Set up your Trash Channel:**
    * Go to Discord, make a channel in server or group where you want the files to go
    * Right-click the channel and click "Copy Channel ID" (you need Developer Mode enabled in Discord settings for this)
    * Open the Settings tab in the app and paste the ID into the **Trash Channel** input
3.  **Upload:** Go to the Upload tab, drag and drop your massive file/folder, select a channel ID where your Manifest is gonna be sent and let it do its thing!
4.  **Download:** Send the Manifest file to your friend as long as they have this app, are logged in, and have access to the Trash Channel on Discord, they can drag the manifest into the Download tab and retrieve the file!

---

*Built with Rust, Tauri, and a mild disregard for file size limits.*
