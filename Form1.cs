using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Installer
{
    public partial class Form1 : Form
    {
        private Stopwatch stopwatch = new Stopwatch(); // To measure download time
        private double downloadSpeedMBps; // Variable to store download speed

        public Form1()
        {
            InitializeComponent();
            string extractionFolderName = "custom folder"; // Name of the extraction folder
            DownloadAndExtractContent(extractionFolderName);
        }

        // Click method to close the application
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // Asynchronous method to download and extract the content
        private async void DownloadAndExtractContent(string folderName)
        {
            string zipUrl = "https://github.com/SteamDatabase/GameTracking-CS2/archive/refs/heads/master.zip"; // URL of the .zip file
            string zipFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "master.zip");
            string extractPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), folderName); // Defines the extraction folder path

            // Check and create the extraction folder if it doesn't exist
            if (!Directory.Exists(extractPath))
            {
                Directory.CreateDirectory(extractPath); // Create the folder
            }

            // ProgressBar settings
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 200; // 100 for download, 100 for extraction

            // Update the status label to show downloading
            label2.Text = "Status: Downloading...";

            if (!File.Exists(zipFilePath))
            {
                try
                {
                    // Start download
                    stopwatch.Start();
                    await DownloadFileAsync(zipUrl, zipFilePath);
                    stopwatch.Stop();
                    MessageBox.Show("Download completed successfully!");

                    // Complete the first half of the progress bar
                    progressBar1.Value = 100;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error downloading the file: {ex.Message}");
                    DeleteFiles(zipFilePath, extractPath); // Delete files on error
                    return;
                }
            }

            try
            {
                // Update the status label to show extraction status
                label2.Text = "Status: Extracting...";

                // Start extraction process (second half of the progress bar)
                ExtractZipFile(zipFilePath, extractPath);
                progressBar1.Value = 200; // Extraction completed
                MessageBox.Show("File extracted successfully!");

                // Delete the .zip file after extraction
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                    MessageBox.Show(".zip file deleted successfully!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting the file: {ex.Message}");
                DeleteFiles(zipFilePath, extractPath); // Delete files on error
            }
        }

        // Method to download the .zip file and update progress and internet speed
        private async Task DownloadFileAsync(string fileUrl, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1; // Handle unknown length
                var buffer = new byte[8192];
                long totalRead = 0L;
                int read;
                long lastBytesRead = 0;
                TimeSpan lastCheck = TimeSpan.Zero;

                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                {
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        totalRead += read;
                        await fileStream.WriteAsync(buffer, 0, read);

                        // Ensure totalBytes is greater than zero for progress calculation
                        if (totalBytes > 0)
                        {
                            int progressValue = (int)(100 * totalRead / totalBytes);
                            progressBar1.Value = Math.Max(0, Math.Min(progressValue, 100)); // Limit to between 0 and 100
                        }
                        else
                        {
                            // If totalBytes is unknown, set progress to -1 for marquee
                            progressBar1.Style = ProgressBarStyle.Marquee;
                        }

                        // Calculate download speed (in MB/s)
                        TimeSpan elapsed = stopwatch.Elapsed - lastCheck;
                        if (elapsed.TotalSeconds > 1)
                        {
                            long bytesPerSecond = (totalRead - lastBytesRead) / (long)elapsed.TotalSeconds;
                            downloadSpeedMBps = bytesPerSecond / (1024.0 * 1024.0); // Store download speed in MB/s

                            // Update label with download speed
                            label3.Text = $"Download Speed: {downloadSpeedMBps:F2} MB/s";

                            // Reset tracking
                            lastBytesRead = totalRead;
                            lastCheck = stopwatch.Elapsed;
                        }
                    }
                }
            }
        }

        // Method to extract the .zip file and update progress for the second half of the progress bar
        private void ExtractZipFile(string zipFilePath, string extractPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedEntries = 0;
                long totalExtractedBytes = 0; // To accumulate extracted bytes
                Stopwatch extractionStopwatch = new Stopwatch(); // To measure extraction time
                extractionStopwatch.Start(); // Start the stopwatch for extraction time

                // Update progress for each entry being extracted
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Create the full path for the entry
                    string entryPath = Path.Combine(extractPath, entry.FullName);

                    // Ensure that the directory exists for the entry
                    string entryDirectory = Path.GetDirectoryName(entryPath);
                    if (!string.IsNullOrEmpty(entryDirectory))
                    {
                        Directory.CreateDirectory(entryDirectory); // Ensure the directory exists
                    }

                    // Extract the file, if it's not a directory
                    if (!string.IsNullOrEmpty(entry.Name)) // Entry.Name is empty for directories
                    {
                        entry.ExtractToFile(entryPath, true);
                        totalExtractedBytes += entry.Length; // Accumulate extracted bytes
                        extractedEntries++;

                        // Update progress bar for extraction (from 100 to 200)
                        progressBar1.Value = 100 + (int)(100 * extractedEntries / totalEntries);
                        label4.Text = $"Extracting: {entry.FullName}"; // Update the label with the current file being extracted

                        // Calculate extraction speed (in MB/s)
                        TimeSpan elapsed = extractionStopwatch.Elapsed;
                        if (elapsed.TotalSeconds > 0)
                        {
                            double extractionSpeedMBps = (totalExtractedBytes / (1024.0 * 1024.0)) / elapsed.TotalSeconds; // Convert bytes to MB and calculate speed
                            label3.Text = $"Extraction Speed: {extractionSpeedMBps:F2} MB/s"; // Update the label with extraction speed
                        }
                    }
                }
            }
        }



        // Method to delete files and folders
        private void DeleteFiles(string zipFilePath, string extractPath)
        {
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true); // Delete the directory and its contents
            }
        }
    }
}
