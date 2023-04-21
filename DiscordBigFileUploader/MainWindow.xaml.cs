using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using Path = System.IO.Path;
using System.Threading;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Ionic.Zip;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Ionic.Zlib;
using static DiscordBigFileUploader.App;
using System.Diagnostics;

namespace DiscordBigFileUploader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string percentCompress = "0.00";

        string sourceFile = "";
        string finalToken = "";
        string finalChannel = "";
        string finalTrashChannel = "";

        IWebDriver driver = null;

        bool isCompressing = false;
        bool isDownloading = false;
        bool isUploading = false;
        bool isUncompressing = false;

        int totalFiles = 0;
        int totalUpload = 0;
        int actualFile = 0;
        int actualDownloaded = 0;

        public MainWindow()
        {
            InitializeComponent();

            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved");
            string filePathTrash = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savedtrash");

            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);
                finalToken = content;
                tokenTextBox.Text = content;
                MessageBox.Show("Token loaded", "Token found!");
            }

            if (File.Exists(filePathTrash))
            {
                string content = File.ReadAllText(filePathTrash);
                finalTrashChannel = content;
                trashChanBox.Text = content;
            }
        }

        private void findToken()
        {
            var DeviceDriver = ChromeDriverService.CreateDefaultService();

            DeviceDriver.HideCommandPromptWindow = true;

            ChromeOptions options = new ChromeOptions();
            options.AddArguments("--disable-infobars");

            driver = new ChromeDriver(DeviceDriver, options);
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl("https://discord.com/login");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(180));
            wait.Until(ExpectedConditions.UrlContains("https://discord.com/channels"));

            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            var getTokenScript = "return (webpackChunkdiscord_app.push([[''],{},e=>{m=[];for(let c in e.c)m.push(e.c[c])}]),m).find(m=>m?.exports?.default?.getToken!==void 0).exports.default.getToken();";
            string token = (string)js.ExecuteScript(getTokenScript);

            finalToken = token;
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved");
            File.WriteAllText(filePath, finalToken);

            driver.Quit();
            MessageBox.Show("Token found and saved!", "Token found!");
        }

        private async void uploadEverythingAsync()
        {
            string appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(appDirectory, "temp");
            string dName = "";
            long baseID = 0;
            List<string> allURL = new List<string>();

            var allFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            totalUpload = Directory.EnumerateFiles(path).Count();
            actualFile = 0;

            foreach (string filePath in allFiles)
            {
                FileInfo file = new FileInfo(@filePath);
                long fileSize = file.Length;
                string fileName = file.Name;
                actualFile++;

                string myJson = "{\"files\":[{\"file_size\":" + fileSize + ",\"filename\":\"" + fileName + "\",\"id\":\"" + new Random().Next(1, 100000) + "\"}]}";

                // FIRST POST
                (string responseContent1, HttpResponseMessage response1) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalTrashChannel + "/attachments", finalToken, new StringContent(myJson, Encoding.UTF8, "application/json"));

                if (responseContent1 == null)
                {
                    MessageBox.Show("Discord send back NULL value", "ERROR");
                    return;
                }

                if (response1.StatusCode == HttpStatusCode.Unauthorized)
                {
                    MessageBox.Show("Invalid Token", "ERROR");
                    return;
                }

                dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent1);
                string uploadUrl = jsonResponse.attachments[0].upload_url;
                string fileNameDiscord = jsonResponse.attachments[0].upload_filename;
                
                // SECOND POST
                var fileContent = new ByteArrayContent(File.ReadAllBytes(@filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-zip-compressed");

                HttpResponseMessage putResponse = await HttpUtil.PutAsync(uploadUrl, fileContent);

                if (putResponse.IsSuccessStatusCode)
                {
                    // THIRD POST
                    string messageJsonFile = "{ \"content\":\"\", \"nonce\":\"" + new Random().Next(262625563, 1862625563) + "\", \"channel_id\":\"" + finalTrashChannel + "\", \"type\":0, \"sticker_ids\":[], \"attachments\":[ { \"id\":\"0\", \"filename\":\"" + fileName + "\", \"uploaded_filename\":\"" + fileNameDiscord + "\" } ] }";
                    (string responseMSG, HttpResponseMessage messageResponse) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalTrashChannel + "/messages", finalToken, new StringContent(messageJsonFile, Encoding.UTF8, "application/json"));
                    JObject jsonObject = JsonConvert.DeserializeObject<JObject>(responseMSG);

                    string fullFilename = jsonObject["attachments"][0]["filename"].ToString();
                    dName = fullFilename.Substring(0, fullFilename.Length - 4);
                    string extension = fullFilename.Substring(fullFilename.LastIndexOf(".z") + 2); ;

                    if (baseID <= 0)
                    {
                        baseID = jsonObject["attachments"][0]["id"].Value<long>();
                        string tempJson = "{\"i\":\"" + 0 + "\",\"e\":\"" + extension + "\"},";
                        allURL.Add(tempJson);
                    }
                    else
                    {
                        string tempJson = "{\"i\":\"" + (jsonObject["attachments"][0]["id"].Value<long>() - baseID).ToString() + "\",\"e\":\""+ extension +"\"},";
                        allURL.Add(tempJson);
                    }
                }
                else
                {
                    loadingLabel.Content = "ERROR, DISCORD \n RETURNED NOT SUCCESS";
                    return;
                }
            }
            string finishedJSON = "{\"urls\":[";
            int index = 0;
            foreach (string url in allURL)
            {
                finishedJSON += url;
                index++;
            }
            finishedJSON = finishedJSON.Substring(0, finishedJSON.Length - 2) + "}],\"ch\":\"" + finalTrashChannel + "\",\"na\":\"" + dName + "\",\"b\":\"" + baseID.ToString("0") + "\"}";

            string fileTxt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dName + ".txt");
            File.WriteAllText(fileTxt, finishedJSON);
            FileInfo fileInfo = new FileInfo(fileTxt);
            long fileSizeInBytes = fileInfo.Length;

            string txtjson = "{\"files\":[{\"file_size\":" + fileSizeInBytes + ",\"filename\":\"" + dName + ".txt" + "\",\"id\":\"20\"}]}";

            (string res, HttpResponseMessage _) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalTrashChannel + "/attachments", finalToken, new StringContent(txtjson, Encoding.UTF8, "application/json"));

            dynamic jsonRes = JsonConvert.DeserializeObject(res);
            string upUrl = jsonRes.attachments[0].upload_url;
            string fnDiscord = jsonRes.attachments[0].upload_filename;

            var jsonCont = new ByteArrayContent(File.ReadAllBytes(@fileTxt));
            jsonCont.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            HttpResponseMessage putRes = await HttpUtil.PutAsync(upUrl, jsonCont);
            if (putRes.IsSuccessStatusCode)
            {
                string msgJsonFile = "{ \"content\":\"\", \"nonce\":\"" + new Random().Next(262625563, 1862625563) + "\", \"channel_id\":\"" + finalChannel + "\", \"type\":0, \"sticker_ids\":[], \"attachments\":[ { \"id\":\"0\", \"filename\":\"" + dName + ".txt" + "\", \"uploaded_filename\":\"" + fnDiscord + "\" } ] }";
                (string resMsg, HttpResponseMessage _) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalChannel + "/messages", finalToken, new StringContent(msgJsonFile, Encoding.UTF8, "application/json"));
                JObject jsonObject = JsonConvert.DeserializeObject<JObject>(resMsg);
            }

            /*int maxLength = 1750;
            List<string> jsonChunks = new List<string>();

            if (finishedJSON.Length > maxLength)
            {
                for (int i = 0; i < finishedJSON.Length; i += maxLength)
                {
                    int chunkLength = Math.Min(maxLength, finishedJSON.Length - i);
                    string jsonChunk = finishedJSON.Substring(i, chunkLength);
                    jsonChunks.Add(jsonChunk);
                }
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dName + ".txt");
                File.WriteAllText(filePath, finishedJSON);
                FileInfo fileInfo = new FileInfo(filePath);
                long fileSizeInBytes = fileInfo.Length;

                string myJson = "{\"files\":[{\"file_size\":" + fileSizeInBytes + ",\"filename\":\"" + dName + ".txt" + "\",\"id\":\"20\"}]}";

                (string responseContent1, HttpResponseMessage response1) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalTrashChannel + "/attachments", finalToken, new StringContent(myJson, Encoding.UTF8, "application/json"));
                
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent1);
                string uploadUrl = jsonResponse.attachments[0].upload_url;
                string fileNameDiscord = jsonResponse.attachments[0].upload_filename;

                var fileContent = new ByteArrayContent(File.ReadAllBytes(@filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
                HttpResponseMessage putResponse = await HttpUtil.PutAsync(uploadUrl, fileContent);
                if (putResponse.IsSuccessStatusCode)
                {
                    string messageJsonFile = "{ \"content\":\"\", \"nonce\":\"" + new Random().Next(262625563, 1862625563) + "\", \"channel_id\":\"" + finalChannel + "\", \"type\":0, \"sticker_ids\":[], \"attachments\":[ { \"id\":\"0\", \"filename\":\"" + dName + ".txt" + "\", \"uploaded_filename\":\"" + fileNameDiscord + "\" } ] }";
                    (string responseMSG, HttpResponseMessage messageResponse) = await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalChannel + "/messages", finalToken, new StringContent(messageJsonFile, Encoding.UTF8, "application/json"));
                    JObject jsonObject = JsonConvert.DeserializeObject<JObject>(responseMSG);
                }
                return;
            }
            else
            {
                jsonChunks.Add(finishedJSON);
            }

            foreach (string jsonChunk in jsonChunks)
            {
                long nonce = new Random().Next(262625563, 1862625563);
                string messageJson = "{\"content\":\"```" + jsonChunk.Replace("\"", "\\\"") + "```\",\"nonce\":\"" + nonce + "\",\"tts\":false,\"flags\":0}";

                using (var client = new HttpClient())
                {
                    await HttpUtil.PostAsync("https://discord.com/api/v9/channels/" + finalChannel + "/messages", finalToken, new StringContent(messageJson, Encoding.UTF8, "application/json"));
                }
            }*/

            Directory.Delete(path, true);
            isUploading = false;
        }

        // ---FUNCTION TO GET THE PERCENT---
        private void checkCompress()
        {
            while (isCompressing)
            {
                this.Dispatcher.Invoke(() =>
                {
                    loadingLabel.Content = "Compressing the file...\n" + percentCompress + "%";
                });

                Thread.Sleep(150);
            }

            Thread threadUpload = new Thread(new ThreadStart(uploadEverythingAsync));
            threadUpload.Start();

            while (isUploading)
            {
                this.Dispatcher.Invoke(() =>
                {
                    loadingLabel.Content = "Uploading Files... \n" + actualFile + "/" + totalUpload ;
                });

                Thread.Sleep(100);
            }
        }

        private void checkDownload()
        {
            while (isDownloading)
            {
                this.Dispatcher.Invoke(() =>
                {
                    downloadingLabel.Content = "Downloading...\n" + actualDownloaded + "/" + totalFiles;
                });

                Thread.Sleep(150);
            }
            while (isUncompressing)
            {
                this.Dispatcher.Invoke(() =>
                {
                    downloadingLabel.Content = "Files Downloaded. \n starting uncompress...";
                });
            }
            this.Dispatcher.Invoke(() =>
            {
                downloadingLabel.Content = "Files Downloaded. \n SUCCESS!";
            });
        }

        private void SendFiles()
        {
            sourceFile = @fileTextBox.Text;
            isCompressing = true;
            isUploading = true;

            finalToken = tokenTextBox.Text;
            finalChannel = channelTextBox.Text;
            finalTrashChannel = trashChanBox.Text;

            string appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string tempDirectory = Path.Combine(appDirectory, "temp");

            Directory.CreateDirectory(tempDirectory);

            DirectoryInfo di = new DirectoryInfo(tempDirectory);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            Thread threadText = new Thread(new ThreadStart(checkCompress));
            threadText.Start();

            Thread thread = new Thread(new ThreadStart(CompressionThread));
            thread.Start();

        }

        private void CompressionThread()
        {
            int chunkSize = 24 * 1024 * 1024;

            string appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string tempDirectory = Path.Combine(appDirectory, "temp");
            Directory.CreateDirectory(tempDirectory);

            byte[] buffer = new byte[chunkSize];

            string fileName = new FileInfo(sourceFile).Name;

            if (Directory.Exists(sourceFile)) // If the sourceFile path is a directory
            {
                using (ZipFile zip = new ZipFile())
                {
                    zip.MaxOutputSegmentSize = 24 * 1024 * 1024;
                    zip.CompressionLevel = CompressionLevel.Level7;
                    zip.CompressionMethod = CompressionMethod.Deflate;
                    zip.CodecBufferSize = 12 * 1024 * 1024;


                    // Recursively add all files in the directory and its subdirectories to the zip file
                    zip.AddDirectory(sourceFile);

                    long totalSize = 0;
                    long TotalTransfer = 0;

                    foreach (string file in Directory.GetFiles(sourceFile, "*", SearchOption.AllDirectories))
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }

                    zip.SaveProgress += (sender, args) =>
                    {
                        // Calculate the progress based on the amount of data compressed
                        TotalTransfer += (long)args.BytesTransferred;
                        double progressPercentage = 100 * (TotalTransfer / (double)totalSize);
                        percentCompress = progressPercentage.ToString("0.00");
                    };

                    zip.Save(tempDirectory + "/" + Path.GetFileName(sourceFile) + ".zip");

                    isCompressing = false;
                }
            }
            else // If the sourceFile path is a file
            {
                using (ZipFile zip = new ZipFile())
                {
                    zip.MaxOutputSegmentSize = chunkSize;
                    zip.CompressionLevel = CompressionLevel.BestCompression;
                    zip.CompressionMethod = CompressionMethod.Deflate;
                    zip.CodecBufferSize = 12 * 1024 * 1024;

                    zip.AddEntry(fileName, File.ReadAllBytes(@sourceFile));

                    zip.SaveProgress += (sender, args) =>
                    {
                        double progressPercentage = 100 * (args.BytesTransferred / (double)args.TotalBytesToTransfer);
                        percentCompress = progressPercentage.ToString("0.00");
                    };

                    zip.Save(tempDirectory + "/" + fileName + ".zip");
                }
            }

            isCompressing = false;
        }

        private void extractAll()
        {
            string downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Download");
            isDownloading = false;
            string[] zipFiles = Directory.GetFiles(downloadPath, "*.zip");
            foreach (string zipfile in zipFiles)
            {
                using (var zip = ZipFile.Read(zipfile))
                {
                    zip.ExtractAll("finished");
                    isUncompressing = false;
                }
            }
            Directory.Delete(downloadPath, true);
        }

        private void downloadAll(JArray urls, string channel, string name, long baseID)
        {
            string downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Download");
            if (urls.Count >= 1)
            {
                JObject url = (JObject)urls.FirstOrDefault();
                long finalId = (long)url["i"] + baseID;
                string id = finalId.ToString("0");
                string eName = (string)url["e"];
                string fullname = name + ".z" + eName;

                string urlDL = "https://cdn.discordapp.com/attachments/" + channel + "/" + id + "/" + fullname + "";

                //MessageBox.Show(urlDL, "URL");
                //return;

                string fileName = Path.GetFileName(urlDL);

                using (WebClient client = new WebClient())
                {
                    client.DownloadFileAsync(new Uri(urlDL), Path.Combine(downloadPath, fileName));
                    client.DownloadFileCompleted += (sender, args) =>
                    {
                        actualDownloaded++;
                        urls.RemoveAt(0);
                        downloadAll(urls, channel, name, baseID);
                    };
                }
            }
            else
            {
                Thread thread = new Thread(new ThreadStart(extractAll));
                thread.Start();
            }
        }

        private void selectFileBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select File or folder";
            ofd.ValidateNames = false;
            ofd.CheckFileExists = false;
            ofd.CheckPathExists = false;
            ofd.FileName = "Folder or File Selection";
            ofd.Filter = "All Files/folders (*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.ShowDialog();

            string folderPath = ofd.FileName;
            if (!File.Exists(ofd.FileName))
            {
                folderPath = Path.GetDirectoryName(ofd.FileName);
            }
            fileTextBox.Text = folderPath;
        }

        private void uploadBtn_Click(object sender, RoutedEventArgs e)
        {
            SendFiles();
        }

        private void downloadBtn_Click(object sender, RoutedEventArgs e)
        {
            JObject jsonDL = JsonConvert.DeserializeObject<JObject>(downloadText.Text);
            JArray urls = (JArray)jsonDL["urls"];

            string downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Download");
            string extractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Finished");
            Directory.CreateDirectory(extractPath);
            Directory.CreateDirectory(downloadPath);

            totalFiles = urls.Count;
            actualDownloaded = 0;

            isDownloading = true;
            isUncompressing = true;

            Thread threadText = new Thread(new ThreadStart(checkDownload));
            threadText.Start();
            
            downloadAll(urls, jsonDL["ch"].ToString(), jsonDL["na"].ToString(), jsonDL["b"].Value<long>());
        }

        private void findTokenBtn_Click(object sender, RoutedEventArgs e)
        {
            if (driver != null)
            {
                return;
            }
            Thread threadDiscord = new Thread(new ThreadStart(findToken));
            threadDiscord.Start();

            while (tokenTextBox.Text == "")
            {
                tokenTextBox.Text = finalToken;
            }
        }

        private void tokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string token = tokenTextBox.Text;
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved");
            File.WriteAllText(filePath, token);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox maCheckBox && maCheckBox.IsChecked == true)
            {
                tokenTextBox.Foreground = Brushes.White;
                return;
            }
            tokenTextBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33));
        }

        private async void findTrashBtn_Click(object sender, RoutedEventArgs e)
        {
            string messageJsonFile = "{\"recipients\":[\"1098057299643342999\"]}";
            (string responseMSG, HttpResponseMessage messageResponse) = await HttpUtil.PostAsync("https://discord.com/api/v9/users/@me/channels", finalToken, new StringContent(messageJsonFile, Encoding.UTF8, "application/json"));
            (string _, HttpResponseMessage _) = await HttpUtil.PostAsync("https://discord.com/api/v9/invites/yNVneZWXxj", finalToken, new StringContent("{\"session_id\":\"78fd4d873f5d1ca29f542288b3f0554a\"}", Encoding.UTF8, "application/json"));

            JObject jsonObject = JsonConvert.DeserializeObject<JObject>(responseMSG);

            trashChanBox.Text = jsonObject["id"].ToString();
            finalTrashChannel = jsonObject["id"].ToString();
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/yNVneZWXxj",
                UseShellExecute = true
            });
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savedtrash");
            File.WriteAllText(filePath, jsonObject["id"].ToString());

        }

        private void trashChanBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            finalTrashChannel = trashChanBox.Text;
        }

        private void loadBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select File";
            ofd.FileName = "Folder or File Selection";
            ofd.Filter = "All Files/folders (*.txt)|*.txt";
            ofd.FilterIndex = 1;
            ofd.ShowDialog();
            string selectedFilePath = ofd.FileName;
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                string fileContent = File.ReadAllText(selectedFilePath);
                downloadText.Text = fileContent;
            }
        }
    }
}
