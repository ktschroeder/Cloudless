using System.Windows.Controls;
using System.Windows;
using WpfAnimatedGif;
using Microsoft.Win32;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WebP.Net;
using System.Collections.Specialized;
using System.Text.Json;
using Path = System.IO.Path;
using System.Diagnostics;
using System.ComponentModel;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private async Task OpenImage()
        {
            string filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp, *.gif, *.webp, *.jfif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.jfif";
            if (Properties.Settings.Default.WebmEnabled)
            {
                filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp, *.gif, *.webp, *.jfif, *.webm)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.jfif;*.webm";
            }
            

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = filter
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadImage(openFileDialog.FileName, true);
                Message("File loaded from dialog.");
            }
        }
        private class NaturalStringComparer : IComparer<string>
        {
            // incorporate the "numerical sorting" used by Windows file explorer, to match the order shown there
            // could also have an option for the pure alphabetization
            // more info: https://www.elevenforum.com/t/enable-or-disable-numerical-sorting-in-file-explorer-in-windows-11.9030/
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int StrCmpLogicalW(string x, string y);

            public int Compare(string? x, string? y)
            {
                if (x == null || y == null) return 0;
                return StrCmpLogicalW(x, y);
            }
        }
        private IComparer<string>? GetComparerForFileNameSorting()
        {
            try  // try/catch in case of issues importing the DLL used for the natural comparer
            {
                return new NaturalStringComparer();
            }
            catch (Exception)
            {  // TODO handle
                return null;
            }
        }
        private void SortImageFilesArray()
        {
            if (imageFiles == null)
                return;

            string sort = Cloudless.Properties.Settings.Default.ImageDirectorySortOrder;

            if (sort.Equals("DateModifiedAscending"))
                imageFiles = imageFiles.OrderBy(s => File.GetLastWriteTime(s)).ToArray();
            else if (sort.Equals("DateModifiedDescending"))
                imageFiles = imageFiles.OrderByDescending(s => File.GetLastWriteTime(s)).ToArray();
            else if (sort.Equals("FileNameAscending"))
                imageFiles = imageFiles.OrderBy(s => s, GetComparerForFileNameSorting()).ToArray();
            else if (sort.Equals("FileNameDescending"))
                imageFiles = imageFiles.OrderByDescending(s => s, GetComparerForFileNameSorting()).ToArray();
            else
                imageFiles = imageFiles.ToArray();

            // loaded image's index may change if sort order changes.
            // getting an updated index prevents awkward jumps when navigating directory
            if (currentlyDisplayedImagePath != null)
            {
                var newIndex = Array.IndexOf(imageFiles, currentlyDisplayedImagePath);
                if (newIndex != -1)  // -1 case is when current image isn't in new directory. Let ancestors handle those cases.
                {
                    currentImageIndex = newIndex;
                }
            }
        }
        private void LoadImagesInDirectory(string directoryPath, bool permitLargeCount = false)
        {
            string[] imageExtensions = { ".JPG", ".JPEG", ".PNG", ".BMP", ".GIF", ".WEBP", ".JFIF" };  // TODO move this and similar lists to a consistent source
            try
            {
                DirectoryInfo di = new DirectoryInfo(directoryPath);
                // Get all files, filter them in-memory, and select their full names
                var files = di.GetFiles()
                              .Where(f => imageExtensions.Contains(f.Extension.ToUpperInvariant()))
                              .Select(f => f.FullName)
                              .ToList();

                if (files.Count > 10 && !permitLargeCount)
                {
                    Message($"Your command would open more than 10 images ({files.Count}) and was stopped. To override this, use: 'o! [directory]'");
                    return;
                }
                foreach (string file in files)
                {
                    var newWindow = new MainWindow(file);
                    newWindow.Show();
                }
            }
            catch (Exception ex)
            {
                Message($"An error occurred: {ex.Message}");
            }
        }
        private async Task LoadImage(string? imagePath, bool openedThroughApp)
        {
            try
            {
                if (imagePath == null)
                    throw new Exception("Path not specified for image.");
                string selectedImagePath = imagePath;

                currentDirectory = Path.GetDirectoryName(selectedImagePath) ?? "";
                var retrievedImageFiles = Directory.GetFiles(currentDirectory, "*.*")
                                      .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".jfif", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
                imageFiles = retrievedImageFiles.ToArray();

                SortImageFilesArray();

                currentImageIndex = Array.IndexOf(imageFiles, selectedImagePath);
                if (currentImageIndex == -1)
                {
                    Message("Image not found at path: " + imagePath);
                    return;
                }

                await DisplayImage(currentImageIndex, openedThroughApp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load the image at path \"{imagePath}\": {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task DisplayImage(int index, bool openedThroughApp)
        {
            RemoveZen();
            autoResizingSpaceIsToggled = false;

            try
            {
                if (index < 0 || imageFiles == null || index >= imageFiles.Length) return;

                var uri = new Uri(imageFiles[index]);

                currentlyDisplayedImagePath = uri.LocalPath;
                AddToRecentFiles(uri.LocalPath);

                if (gifController != null)
                {
                    gifController.Dispose();
                    gifController = null;  // can probably more efficiently reuse this. see https://github.com/XamlAnimatedGif/WpfAnimatedGif/blob/master/WpfAnimatedGif.Demo/MainWindow.xaml.cs
                }

                if (uri.AbsolutePath.ToLower().EndsWith(".gif"))
                {
                    var bitmap = new BitmapImage(uri);
                    ImageDisplay.Source = bitmap;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.
                    ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);

                    gifController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF
                }
                else if (uri.AbsolutePath.ToLower().EndsWith(".webm"))
                {
                    if (Properties.Settings.Default.WebmEnabled)
                    {
                        try
                        {
                            // TODO here, show "loading WEBM..." persistent message on solid black

                            var path = uri.OriginalString;
                            if (!File.Exists(path))
                                return;

                            string convertedGifPath = GetFilePathForWebmGifConversion();
                            Random random = new Random();

                            if (!File.Exists(convertedGifPath))
                            {
                                string directory = Path.GetTempPath();
                                string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
                                string tempThumbPath = Path.Combine(cloudlessTempPath, $"ThumbTemp{random.Next(int.MaxValue)}.jpg");

                                int height = -1;
                                int width = -1;

                                string ffmpegThumbArgs = $"-i \"{path}\" -frames:v 1 \"{tempThumbPath}\"";
                                var ffmpeg = new FFmpegExecutor();
                                await ffmpeg.ExecuteFFmpegCommand(ffmpegThumbArgs, this);
                                using (var thumb = new Bitmap(tempThumbPath))
                                {
                                    height = thumb.Height;
                                    width = thumb.Width;
                                }
                                File.Delete(tempThumbPath);

                                int convertedWidth = Math.Min(width, 500);

                                string ffmpegArgs = $"-i \"{path}\" -vf \"scale=-1:{convertedWidth}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{convertedGifPath}\"";
                                await ffmpeg.ExecuteFFmpegCommand(ffmpegArgs, this);
                            }

                            // TODO it would be nice if we could do this in an async manner, currently it freezes UI
                            var bitmap2 = new BitmapImage(new Uri(convertedGifPath));
                            ImageDisplay.Source = bitmap2;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.
                            ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap2);  // This is the slow method

                            gifController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF
                        }
                        catch (Exception ex)
                        {
                            Message($"An error occurred: {ex.Message}");
                        }
                    }
                    else  // WEBMs disabled
                    {
                        Message("Tried to load WEBM, but WEBMs are disabled. See preferences.");
                    }
                }
                else if (uri.AbsolutePath.ToLower().EndsWith(".webp"))
                {
                    byte[] webpBytes = File.ReadAllBytes(imageFiles[index]);
                    using var webp = new WebPObject(webpBytes);
                    var webpImage = webp.GetImage();
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        ((Bitmap)webpImage).GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                    ImageDisplay.Source = bitmapSource;
                }
                else
                {
                    var bitmap = new BitmapImage(uri);
                    ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                    ImageDisplay.Source = bitmap;
                }

                // hide the no-image message if an image is loaded
                ImageDisplay.Visibility = Visibility.Visible;
                if (NoImageMessage != null)
                    NoImageMessage.Visibility = Visibility.Collapsed;

                if (!openedThroughApp || Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp)
                {
                    ResizeWindowToImage();
                    CenterWindow();
                }

                ApplyDisplayMode();
                UpdateContextMenuState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to display image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CopyCompressedImageToClipboardAsJpgFile()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                Message("No image is currently loaded for you to copy.");
                return;
            }

            if (currentlyDisplayedImagePath?.ToLower().EndsWith(".gif") ?? false)
            {
                Message("This app does not support compressing GIFs.");
                return;
            }

            string tempFilePath = GetUniqueCompressedFilePath();
            double maxSizeInMB = Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB;

            try
            {
                long finalSizeBytes = -1;
                long finalQuality = 100;

                // Define maximum size in bytes
                double maxSizeInBytes = maxSizeInMB * 1024 * 1024;

                // Convert BitmapSource to Bitmap
                using (Bitmap bitmap = BitmapSourceToBitmap(bitmapSource))
                {
                    using (Bitmap compatibleBitmap = new(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(compatibleBitmap))
                        {
                            g.DrawImage(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        }

                        const long qualityStep = 5L;
                        long quality = 100L;
                        const long minQuality = 10L; // Minimum quality to avoid over-compression

                        bool compressed = false;

                        while (!compressed)
                        {
                            using (MemoryStream memoryStream = new())
                            {
                                ImageCodecInfo? jpegCodec = GetEncoder(ImageFormat.Jpeg);
                                if (jpegCodec == null)
                                    throw new Exception("Failed to get JPEG codec");

                                EncoderParameters encoderParams = new(1);
                                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                                compatibleBitmap.Save(memoryStream, jpegCodec, encoderParams);

                                // Check the file size
                                if (memoryStream.Length <= maxSizeInBytes)
                                {
                                    finalSizeBytes = memoryStream.Length;
                                    finalQuality = quality;

                                    // Save the compressed image to the temporary file
                                    // TODO clean these up so they don't accumulate?
                                    File.WriteAllBytes(tempFilePath, memoryStream.ToArray());
                                    compressed = true;
                                }
                                else
                                {
                                    // Reduce quality for further compression
                                    quality -= qualityStep;
                                    if (quality < minQuality)
                                        throw new Exception("Unable to compress image to fit the size limit");
                                }
                            }
                        }
                    }
                }

                CopyImageAtPathToClipboard(tempFilePath);

                Message($"Copied compressed file to clipboard: {tempFilePath}. Quality: {finalQuality}%. Bytes: {finalSizeBytes}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy compressed image as file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetUniqueCompressedFilePath()
        {
            string directory = Path.GetTempPath();
            string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
            if (!Directory.Exists(cloudlessTempPath))
                Directory.CreateDirectory(cloudlessTempPath);

            string? originalFileName = Path.GetFileNameWithoutExtension(currentlyDisplayedImagePath);
            string extension = ".jpg";
            int index = 0;

            string filePath;
            do
            {
                filePath = Path.Combine(cloudlessTempPath, $"{originalFileName}-compressed-{index}{extension}");
                index++;
            } while (File.Exists(filePath));

            return filePath;
        }

        private string GetFilePathForWebmGifConversion()
        {
            string directory = Path.GetTempPath();
            string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
            if (!Directory.Exists(cloudlessTempPath))
                Directory.CreateDirectory(cloudlessTempPath);

            var webmGifDict = GetWebmGifConversionMap();
            if (webmGifDict.TryGetValue(currentlyDisplayedImagePath ?? "", out string? gifName))
                return Path.Combine(cloudlessTempPath, gifName);

            string originalFileName = Path.GetFileNameWithoutExtension(currentlyDisplayedImagePath ?? "");
            string extension = ".gif";
            int index = 0;

            string filePath;
            do
            {
                filePath = Path.Combine(cloudlessTempPath, $"{originalFileName}_{index}{extension}");
                index++;
            } while (File.Exists(filePath));

            webmGifDict.Add(currentlyDisplayedImagePath ?? "", Path.GetFileNameWithoutExtension(filePath) + ".gif");
            UpdateWebmGifConversionMap(webmGifDict);

            return filePath;
        }

        private Dictionary<string, string> GetWebmGifConversionMap()  // (WEBM full path, GIF filename)
        {
            string directory = Path.GetTempPath();
            string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
            string mapFile = Path.Combine(cloudlessTempPath, "WebmGifConversionMap.json");

            Dictionary<string, string>? dict = null;

            if (File.Exists(mapFile))
            {
                string text = File.ReadAllText(mapFile);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            }

            return dict ?? new Dictionary<string, string>();
        }

        private void UpdateWebmGifConversionMap(Dictionary<string, string> map)
        {
            string directory = Path.GetTempPath();
            string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
            string mapFile = Path.Combine(cloudlessTempPath, "WebmGifConversionMap.json");

            var str = JsonSerializer.Serialize(map);
            File.WriteAllText(mapFile, str);
        }

        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            using var ms = new MemoryStream();
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return new Bitmap(ms);
        }
        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
        private bool IsSupportedImageFile(string filePath)
        {
            string? extension = Path.GetExtension(filePath)?.ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp" || extension == ".gif" || extension == ".webp" || extension == ".jfif" || (Properties.Settings.Default.WebmEnabled && extension == ".webm");
        }
        private bool IsSupportedImageUri(Uri uri)
        {
            string? extension = Path.GetExtension(uri.LocalPath)?.ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp" || extension == ".gif" || extension == ".webp" || extension == ".jfif" || (Properties.Settings.Default.WebmEnabled && extension == ".webm");
        }
        private async void DownloadAndLoadImage(Uri uri)
        {
            try
            {
                using HttpClient client = new HttpClient();
                byte[] imageData = await client.GetByteArrayAsync(uri);
                using MemoryStream stream = new MemoryStream(imageData);

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImageDisplay.Source = bitmap;

                // Show the image and hide the no-image message
                ImageDisplay.Visibility = Visibility.Visible;
                if (NoImageMessage != null) 
                    NoImageMessage.Visibility = Visibility.Collapsed;

                ApplyDisplayMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image from URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CopyImageToClipboard()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                Message("No image is currently loaded for you to copy.");
                return;
            }

            try
            {
                if (!File.Exists(currentlyDisplayedImagePath))
                {
                    MessageBox.Show("Image file does not exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                    // could just copy bitmap in this case
                }

                CopyImageAtPathToClipboard(currentlyDisplayedImagePath);
                Message("Copied image file to clipboard.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyImageAtPathToClipboard(string imagePath)
        {
            var bitmap = new Bitmap(imagePath);
            var dataObject = new DataObject();

            // Add the file path for file-pasting scenarios (e.g., Discord, file explorer)
            var filePaths = new StringCollection
                {
                    imagePath
                };
            dataObject.SetFileDropList(filePaths);

            // Add the bitmap for image-pasting scenarios (e.g., MS Paint)
            dataObject.SetData(DataFormats.Bitmap, bitmap);

            Clipboard.SetDataObject(dataObject, true);
        }

        private void AddToRecentFiles(string filePath)
        {
            // Avoid duplicates
            recentFiles.Remove(filePath);
            recentFiles.Insert(0, filePath);

            // Enforce max size
            if (recentFiles.Count > MaxRecentFiles)
                recentFiles.RemoveAt(recentFiles.Count - 1);

            SaveRecentFiles();

        }
        private void PrepareZoomMenu()
        {
            int[] roundZooms = { 10, 25, 50, 75, 100, 150, 200, 400, 800 };

            // Clear the existing items
            ZoomMenu.Items.Clear();

            // TODO could add things like "fit window" here

            // Populate the menu
            foreach (int zoom in roundZooms)
            {
                MenuItem zoomItem = new MenuItem
                {
                    Header = $"{zoom}%",
                    ToolTip = zoom,
                    Tag = zoom
                };
                zoomItem.Click += (s, e) =>
                {
                    var tag = ((MenuItem)s).Tag;
                    int zoom = (int)tag;
                    double scale = (double)zoom / 100d;
                    ZoomFromCenterToGivenScale(scale);
                };
                ZoomMenu.Items.Add(zoomItem);
            }
        }

        public static System.Windows.Controls.Image? GetImageThumbnail(string filePath, int width, int height, bool isContextWindow = false)
        {
            try  // called 10+ times every time context menu is used/updated. Could be more efficient.
            {
                using var stream = File.OpenRead(filePath);

                var bitmap = new BitmapImage();

                // The former below is faster for the context window, and the latter is more compatible with the secondary window thumbnails.
                // If we only use the latter, then it seriously slows down normal image loading since it hits slower paths. This could be improved
                // with more async re-working in the future, but for now this works well.
                if (isContextWindow)
                {
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.DecodePixelWidth = width;
                    bitmap.DecodePixelHeight = height;
                    bitmap.EndInit();
                }
                else
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = width;
                    bitmap.DecodePixelHeight = height;  // can remove this line to get preserved aspect ratio with image cut off
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();  // CRITICAL
                }

                return new System.Windows.Controls.Image { Source = bitmap, Width = width, Height = height };
            }
            catch (Exception ex)  // TODO WEBMs will get us here
            {
                Console.WriteLine($"Failed to load thumbnail: {ex.Message}");
                return new System.Windows.Controls.Image { Source = null, Width = width, Height = height }; ;  // Return null if there's an issue loading the image
            }
        }
        private void UpdateRecentFilesMenu()  // no side effects beyond instance. Reads from static file at this time, does not write to it.
        {
            LoadRecentFiles(); // Always fetch the latest list

            // Clear the existing items
            RecentFilesMenu.Items.Clear();

            // Populate the menu
            foreach (string file in recentFiles)
            {
                MenuItem fileItem = new MenuItem
                {
                    Header = System.IO.Path.GetFileName(file),
                    ToolTip = file,
                    Tag = file,
                    Icon = GetImageThumbnail(file, 16, 16, true)
                };
                fileItem.Click += async (s, e) => await OpenRecentFile((string)((MenuItem)s).Tag);
                RecentFilesMenu.Items.Add(fileItem);
            }

            // Add additional menu items
            if (recentFiles.Count > 0)
            {
                MenuItem openGalleryItem = new()
                {
                    Header = "Open Recent Images Gallery"
                };
                openGalleryItem.Click += (s, e) =>
                {
                    OpenRecentImagesWindow();
                };

                RecentFilesMenu.Items.Insert(0, openGalleryItem);
                RecentFilesMenu.Items.Insert(1, new Separator());


                RecentFilesMenu.Items.Add(new Separator());

                MenuItem clearHistoryItem = new MenuItem
                {
                    Header = "Clear History",
                    ToolTip = "Clear the list of recent files."
                };
                clearHistoryItem.Click += (s, e) => ClearRecentFiles();
                RecentFilesMenu.Items.Add(clearHistoryItem);
            }
            else
            {
                MenuItem noRecentFilesItem = new()
                {
                    Header = "No Recent Files",
                    IsEnabled = false
                };
                RecentFilesMenu.Items.Add(noRecentFilesItem);
            }
        }

        public async Task OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadImage(filePath, true);
        }
        private void SaveRecentFiles()
        {
            if (recentFilesMutex.WaitOne(2000))  // Wait for up to 2 seconds
            {
                try
                {
                    // Ensure the directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(recentFilesPath) ?? "");

                    File.WriteAllText(recentFilesPath, System.Text.Json.JsonSerializer.Serialize(recentFiles));
                }
                finally
                {
                    recentFilesMutex.ReleaseMutex();
                }
            }
            else
            {
                MessageBox.Show("Unable to write to recent files. Another instance may be busy.");  // TODO should this and other similar lines be replaced with Message()? These might be outdated.
            }
        }
        private void RecentFilesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            LoadRecentFiles();
            UpdateRecentFilesMenu();
        }

        private void LoadRecentFiles() // TODO handle exceptions: file corruption, access issues, launching with empty or missing list, manually deleting file outside of or inside of session(s).
        {
            if (recentFilesMutex.WaitOne(2000)) // Wait for up to 2 seconds
            {
                try
                {
                    if (File.Exists(recentFilesPath))
                    {
                        string json = File.ReadAllText(recentFilesPath);
                        recentFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    }
                    else
                    {
                        recentFiles = new List<string>();
                    }
                }
                finally
                {
                    recentFilesMutex.ReleaseMutex();
                }
            }
            else
            {
                MessageBox.Show("Unable to access recent files. Another instance may be busy.");
            }
        }

        private void ClearRecentFiles()
        {
            recentFiles.Clear();
            SaveRecentFiles();
            UpdateRecentFilesMenu();
        }

        async Task<GitHubRelease?> GetLatestReleaseAsync(bool allowPrerelease)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Cloudless");

            var json = await client.GetStringAsync(
                "https://api.github.com/repos/ktschroeder/Cloudless/releases");

            var releases = System.Text.Json.JsonSerializer.Deserialize<List<GitHubRelease>>(json);

            return releases?
                .Where(r => !r.draft)
                .Where(r => allowPrerelease || !r.prerelease)
                .OrderByDescending(r => r.published_at)
                .FirstOrDefault();
        }


        public static bool IsNewerVersion(string latestTag, string currentVersion)
        {
            latestTag = latestTag.TrimStart('v', 'V');

            if (!Version.TryParse(latestTag, out var latest))
                return false;

            if (!Version.TryParse(currentVersion, out var current))
                return false;

            return latest > current;
        }

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                var release = await GetLatestReleaseAsync(true);
                if (release == null)
                    return;

                string currentVersion = CURRENT_VERION;

                if (IsNewerVersion(release.tag_name, currentVersion))
                {
                    //ShowUpdateAvailable(release);
                    Message($"A newer version of Cloudless ({Version.Parse(release.tag_name.TrimStart('v', 'V')).ToString()}) is available!");
                }
            }
            catch (Exception)
            {
                //Message(e.ToString());
                // Fail silently — update checks should NEVER crash the app
            }
        }
    }

    public class GitHubRelease
    {
        public string tag_name { get; set; } = "";
        public string html_url { get; set; } = "";
        public bool prerelease { get; set; }
        public bool draft { get; set; }
        public DateTime published_at { get; set; }
    }

    public class FFmpegExecutor
    {
        // returns whether successful
        public async Task<bool> ExecuteFFmpegCommand(string ffmpegArguments, MainWindow mainWindow)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg", // "ffmpeg.exe" on Windows, "ffmpeg" on Linux/macOS, relies on the system PATH
                Arguments = ffmpegArguments,
                UseShellExecute = false, // Must be false to redirect I/O streams
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Often FFmpeg output is on StandardError
                CreateNoWindow = true
            };

            Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    // process can be null if ffmpeg is not installed?
                    if (process == null)
                        throw new Win32Exception("process was null");

                    // Capture output (optional, but useful for debugging and progress monitoring)
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"[FFmpeg OUT] {e.Data}");
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // FFmpeg progress usually comes through the error stream
                            Console.WriteLine($"[FFmpeg ERR] {e.Data}");
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for the process to exit
                    await process.WaitForExitAsync(); // Use WaitForExitAsync() for async

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("FFmpeg command executed successfully.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"FFmpeg command failed with exit code: {process.ExitCode}");
                        mainWindow.Message($"Failed to convert WEBM due to ffmpeg error.");
                        return false;
                    }
                }
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                mainWindow.Message($"Failed to convert WEBM: FFmpeg is not installed or cannot be found.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                mainWindow.Message("Failed to convert WEBM due to unexpected error.");
                return false;
            }
        }
    }
}
