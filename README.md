# 🎊 Discord Big File Sender (Without Nitro) 🎊
 This program allows you to upload files of any size on Discord (without Nitro) and download them with the app.


# 🖼️ Screenshots
![image](https://user-images.githubusercontent.com/34484331/233513344-ccafb2bc-4bf6-4f11-b329-2f87dee2a621.png)
![image](https://user-images.githubusercontent.com/34484331/233513362-62461f23-2a9c-4d20-9974-5e82e963ded3.png)
![image](https://user-images.githubusercontent.com/34484331/233513588-f24b86de-3186-4ec5-96a9-0f443ea2c5b5.png)


# 👍 How to use it?
To use this program, follow these steps:
- First, you need to obtain your Discord Token. Click the "Auto Token" button to get it automatically. Then, log into your Discord account, and the program will save your Discord Token.
- Next, enable Developer mode on your Discord app by going to Settings -> Advanced -> Developer mode ON.
- Select a channel/PM where you want to send the files, right-click on it, and click "Copy ID" at the bottom. Paste it into the Channel ID Text Box.
- You also need a Trash Channel ID where all your files will be uploaded. Click on "Default", and the program will make you join a Discord server, create a PM with a trash account that I have made (I won't be monitoring the contents of your uploads), and save the ID of this PM channel.
- Choose the file/folder you want to upload.
- Click Upload and voila!

To download someone file you just need to download the txt file you received and click on "Load File" choose the file and then click "Download"


# 💖 Features
- Upload files of any size
- Upload files of any extension
- Fast Compression (Using the basic Deflate with a 12MB dictionary)
- Pretty "fast" uploading speed (Still limited by discord)
- Files saved on Discord (as far as I know, Discord does not delete files)
- User-friendly interface (Very Basic)
- Pretty "fast" downloads
- No Nitro subscription required 😎

# 🤓 How does it work?
It compresses the file (or folder) of your choice into multiple 24MB zip files, and sends each compressed file to a designated 'Trash Channel'. After all the files have been uploaded, a text file is sent to the people you wish to send the files to. This file contains a JSON code that they can copy and use on the app to download and automatically extract all the files!


# NuGet used
- DotNetZip
- Selenium.Support
- Newtonsoft.Json
- Selenium.WebDriver
- Selenium.WebDriver.ChromeDriver


# 👤 Contact
You can contact me on Discord "Reiko <3#8698" if you have any questions!
