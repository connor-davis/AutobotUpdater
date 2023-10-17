using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Octokit;
using FileMode = System.IO.FileMode;

namespace AutobotUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static MainWindow _instance;

        public MainWindow()
        {
            InitializeComponent();

            _instance = this;

            DownloadStatus.Text = "Initializing update.";

            new Task(DownloadLatest).Start();
        }

        public static MainWindow GetInstance()
        {
            return _instance;
        }

        private async void DownloadLatest()
        {
            var gitHubClient = new GitHubClient(new ProductHeaderValue("connor-davis"));
            var releases = await gitHubClient.Repository.Release.GetAll("connor-davis", "Autobotv4");
            var latestGithubVersion = new Version(releases[0].TagName.Replace("v", ""));
            var downloadUrl =
                $"https://github.com/connor-davis/Autobotv4/releases/download/v{latestGithubVersion}/Autobotv4.zip";

            using var httpClient = new HttpClient();

            try
            {
                await using var fileStream =
                    new FileStream($".\\Autobot.zip", FileMode.Create, FileAccess.Write, FileShare.None);

                Dispatcher.Invoke(() => { DownloadStatus.Text = "Downloading update."; });

                // Send an HTTP GET request to the URL and get the response
                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                // Check if the response is successful
                response.EnsureSuccessStatusCode();

                // Get the content length (file size) from the response headers
                var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();

                // Create a buffer for downloading data in chunks
                var buffer = new byte[8192];
                long bytesRead = 0;

                // Create a stream to read the response content
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                int bytesReadThisChunk;

                while ((bytesReadThisChunk = await contentStream.ReadAsync(buffer)) > 0)
                {
                    // Write the downloaded data to the file
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesReadThisChunk));

                    // Update the progress bar and status text
                    bytesRead += bytesReadThisChunk;

                    var progress = (double)bytesRead / totalBytes * 100;

                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = progress;
                        DownloadStatus.Text = $"Downloading... {progress:F2}%";
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    DownloadStatus.Text = "Download finished.";
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                });

                fileStream.Close();
                httpClient.Dispose();

                Thread.Sleep(5000);

                new Task(Install).Start();
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadStatus.Text = "Please wait an hour before updating again. You have reached your limit.";
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                });

                Thread.Sleep(2000);

                Environment.Exit(0);
            }
        }

        private void Install()
        {
            Dispatcher.Invoke(() => { DownloadStatus.Text = "Installing update."; });

            try
            {
                try
                {
                    ZipFile.ExtractToDirectory($".\\Autobot.zip", $".\\temp", true);
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadStatus.Text = "Failed to extract update.";
                        DownloadProgressBar.Visibility = Visibility.Collapsed;

                        Environment.Exit(0);
                    });
                }

                Dispatcher.Invoke(() => { DownloadStatus.Text = "Cleaning up."; });

                if (CopyUpdate($".\\temp", $"."))
                {
                    if (DeleteUpdate($".\\temp"))
                    {
                        Thread.Sleep(2000);

                        if (File.Exists($".\\Autobot.zip")) File.Delete($".\\Autobot.zip");

                        Dispatcher.Invoke(() => { DownloadStatus.Text = "Installation complete."; });

                        Thread.Sleep(2000);

                        Dispatcher.Invoke(() => { DownloadStatus.Text = "Launching Autobot."; });

                        Thread.Sleep(2000);

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = ".\\Autobot.exe",
                            UseShellExecute = true,
                            CreateNoWindow = true
                        };

                        Process.Start(startInfo);

                        Dispatcher.Invoke(() => { Environment.Exit(0); });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadStatus.Text = "Failed to delete update.";
                            DownloadProgressBar.Visibility = Visibility.Collapsed;
                        });

                        Thread.Sleep(2000);

                        Dispatcher.Invoke(() => { Environment.Exit(0); });
                    }
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadStatus.Text = "Failed to copy update.";
                        DownloadProgressBar.Visibility = Visibility.Collapsed;
                    });

                    Thread.Sleep(2000);

                    Dispatcher.Invoke(() => { Environment.Exit(0); });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadStatus.Text = ex.Message;
                    DownloadProgressBar.Visibility = Visibility.Collapsed;

                    Thread.Sleep(2000);

                    Console.WriteLine($"Error: {ex.Message}");
                });
            }
        }

        private static bool CopyUpdate(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var files = Directory.GetFiles(sourceDir);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);

                try
                {
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                        File.Copy(file, destFile);
                    }
                    else
                    {
                        File.Copy(file, destFile);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var subdirectories = Directory.GetDirectories(sourceDir);

            foreach (var subDir in subdirectories)
            {
                var subDirName = new DirectoryInfo(subDir).Name;
                var destSubDir = Path.Combine(targetDir, subDirName);

                CopyUpdate(subDir, destSubDir);
            }

            return true;
        }

        private static bool DeleteUpdate(string sourceDir)
        {
            var files = Directory.GetFiles(sourceDir);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(sourceDir, fileName);

                File.Delete(destFile);
            }

            var subdirectories = Directory.GetDirectories(sourceDir);

            foreach (var subDir in subdirectories)
            {
                var subDirName = new DirectoryInfo(subDir).Name;
                var destSubDir = Path.Combine(sourceDir, subDirName);

                DeleteUpdate(destSubDir);
            }

            Directory.Delete(sourceDir);

            return true;
        }
    }
}