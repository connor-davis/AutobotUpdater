using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Octokit;
using FileMode = System.IO.FileMode;

namespace AutobotUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static MainWindow? _instance;

        private static readonly string? DirectoryPath =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public MainWindow()
        {
            InitializeComponent();

            _instance = this;

            new Task(DownloadLatest).Start();
        }

        public static MainWindow? GetInstance()
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
                    new FileStream("Autobot.zip", FileMode.Create, FileAccess.Write, FileShare.None);

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

                fileStream.Close();

                Dispatcher.Invoke(() =>
                {
                    DownloadStatus.Text = "Download Finished.";

                    Thread.Sleep(1000);

                    new Task(Install).Start();
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void Install()
        {
            Dispatcher.Invoke(() => { DownloadStatus.Text = "Installing Update."; });

            try
            {
                ZipFile.ExtractToDirectory("Autobot.zip", $"{DirectoryPath!}/temp", true);

                Dispatcher.Invoke(() =>
                {
                    var copyStartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        RedirectStandardOutput = false,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var copyProcess = new Process { StartInfo = copyStartInfo };

                    copyProcess.Start();
                    copyProcess.StandardInput.WriteLine(@"for /r . %a in (*) do copy /b/v/y %a ..");
                    copyProcess.StandardInput.WriteLine("exit");
                    copyProcess.WaitForExit();
                    
                    DownloadStatus.Text = "Installation Complete.";
                    
                    Thread.Sleep(1000);

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "Autobot.exe",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process.Start(startInfo);

                    Environment.Exit(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    Width = 1920;
                    DownloadStatus.Text = ex.Message;
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                });
                
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}