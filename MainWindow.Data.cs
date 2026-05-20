using System.Windows.Controls;
using System.Windows;
//using WpfAnimatedGif;
using AnimatedImage;
using Microsoft.Win32;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Collections.Specialized;
using System.Text.Json;
using Path = System.IO.Path;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Threading;
using System.Windows.Shapes;
using System;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using static System.Net.WebRequestMethods;
using System.Net.Http.Headers;
using File = System.IO.File;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Configuration;
using AnimatedImage.Wpf;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private bool _openDialogInProgress = false;  // lazy band-aid for odd bug where opening a WEBM from file dialog causes another Key.O to be registered, opening a second file dialog.
        private async Task OpenImage()
        {
            if (_openDialogInProgress)
                return;

            _openDialogInProgress = true;

            try
            {
                string filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp, *.gif, *.webp, *.jfif, *.webm, *.mkv, *.mp4)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.jfif;*.webm;*.mkv;*.mp4";
                
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
            finally
            {
                _openDialogInProgress = false;
            }
        }
        public async Task<string?> SelectWorkspaceFileToPreview()
        {
            string filter = "Image files (*.cloudless)|*.cloudless";
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = workspaceFilesPath
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string workspaceName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                return workspaceName;
            }
            return null;
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
            catch (Exception ex)
            {
                Message("Error preparing sorting: " + ex.Message);
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
                _currentImageEverCropped = false;

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
                                                 s.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
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
                Message($"Failed to load the image at path \"{imagePath}\": {ex.Message}");
            }
        }
        private async Task DisplayImage(int index, bool openedThroughApp)
        {
            RemoveZen();
            autoResizingSpaceIsToggled = false;
            imageOriginalWorkspaceName = null;  // reset this whenever an image is loaded, e.g. left/right iteration. When loading workspaces, we define this in post-process.

            VideoHost.Height = 0;
            VideoHost.Width = 0;
            // TODO dispose? maybe in plugin method?

            try
            {
                if (index < 0 || imageFiles == null || index >= imageFiles.Length) return;

                var uri = new Uri(imageFiles[index]);

                currentlyDisplayedImagePath = uri.LocalPath;
                AddToRecentFiles(uri.LocalPath);

                animationController = ImageBehavior.GetAnimationController(ImageDisplay);
                if (animationController != null)
                {
                    animationController.Dispose();
                    animationController = null;  // can probably more efficiently reuse this. see https://github.com/XamlAnimatedGif/WpfAnimatedGif/blob/master/WpfAnimatedGif.Demo/MainWindow.xaml.cs
                }

                if (VideoHost.Content is Cloudless.PluginBase.IVideoPlayer videoPlayer)
                {
                    videoPlayer.Stop();
                    videoPlayer.Dispose();
                }

                if (ImageDisplay.Source is BitmapImage bi)
                {
                    bi.StreamSource?.Dispose();
                }

                //System.GC.Collect();

                if (uri.AbsolutePath.ToLower().EndsWith(".gif"))  // TODO fair bit of duplicate code shared with WEBM section of this method
                {
                    var fileSizeMB = (double)(new FileInfo(uri.OriginalString).Length) / 1024 / 1024;
                    bool loadGif = true;

                    if (fileSizeMB > 50d)  // TODO maybe make this max configurable, and/or option to disable this warning. also magic number
                    {
                        var gifWarningWindow = new GifWarningWindow(currentlyDisplayedImagePath, fileSizeMB)
                        {
                            Owner = this,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        gifWarningWindow.ShowDialog();
                        loadGif = gifWarningWindow.Proceed;
                    }
                    
                    if (loadGif)
                    {
                        ShowLoadingOverlay($"Loading GIF... ({(int)fileSizeMB} MB)", $"{Path.GetFileName(uri.AbsolutePath)}");
                        await Dispatcher.Yield(DispatcherPriority.Background);

                        BitmapImage bitmap;
                        if (_preloadManager != null && _preloadManager.TryGet(uri.LocalPath, out var cachedGif))
                        {
                            bitmap = cachedGif;
                        }
                        else
                        {
                            var tmp = new BitmapImage();
                            tmp.BeginInit();
                            tmp.UriSource = uri;
                            tmp.CacheOption = BitmapCacheOption.OnLoad;
                            tmp.EndInit();
                            tmp.Freeze();
                            bitmap = tmp;
                        }


                        ImageDisplay.Source = bitmap;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.

                        await Dispatcher.Yield(DispatcherPriority.Background);
                        ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);  // slow method and cannot be made async
                        HideLoadingOverlay();


                        animationController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF // tag GIFNULL
                    }
                    else
                    {
                        ImageDisplay.Source = null;  // show black screen rather than previous image to minimize confusion. TODO can improve on this UX probably.
                    }
                 }
                else if (uri.AbsolutePath.ToLower().EndsWith(".webm") || uri.AbsolutePath.ToLower().EndsWith(".mkv") || uri.AbsolutePath.ToLower().EndsWith(".mp4"))
                {
                    try
                    {
                        var plugin = PluginManager.GetPluginForFiletype("webm");
                        if (plugin == null)
                        {
                            Message("Load failed: No plugin found for WEBM/MKV/MP4 files, or version is too old. See plugin tab in preferences window.");
                            return;
                        }

                        UIElement? view;
                        ShowLoadingOverlay($"Waiting for VLC plugin to initialize...");
                        //await Dispatcher.Yield(DispatcherPriority.Background);
                        view = await plugin.CreateView();  // We wait here a while if a WEBM is opened quickly in a fresh Cloudless instance/process. Plugin takes several seconds to init.
                        HideLoadingOverlay();

                        VideoHost.Content = view;

                        VideoHost.Height = double.NaN;
                        VideoHost.Width = double.NaN;
                        //VideoHost.Height = 300;
                        //VideoHost.Width = 300;

                        if (VideoHost.Content is Cloudless.PluginBase.IVideoPlayer player)
                        {
                            
                            Task postPlayTask = new Task(async () =>  // sync, to do an elegant concurrency dance with the below play method
                            {
                                try
                                {
                                    await ResizeWindowToImage();
                                    CenterWindowOnCurrentScreen();
                                }
                                catch (Exception ex)
                                {
                                    Dispatcher.Invoke(() => Message($"Failed to get video dimensions from plugin: {ex.Message}"));
                                }
                            });

                            Task.Run(() => player.Play(uri, postPlayTask));  // sync, to avoid thread issues that occurred when using async play method

                        }  // TODO else?
                        ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                        ImageDisplay.Source = null;
                    }
                    catch (Exception ex)
                    {
                        Message($"Failed to load file: {ex.Message}");
                    }
                }
                else if (uri.AbsolutePath.ToLower().EndsWith(".webp"))
                {
                    BitmapImage bitmap;
                    if (_preloadManager != null && _preloadManager.TryGet(uri.LocalPath, out var cachedWebp))
                    {
                        bitmap = cachedWebp;
                    }
                    else
                    {
                        var tmp = new BitmapImage();
                        tmp.BeginInit();
                        tmp.UriSource = uri;
                        tmp.CacheOption = BitmapCacheOption.OnLoad;
                        tmp.EndInit();
                        tmp.Freeze();
                        bitmap = tmp;
                    }

                    ImageDisplay.Source = bitmap;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.

                    var animatedWebpPlugin = PluginManager.GetPluginForFiletype("webp");
                    if (animatedWebpPlugin != null && animatedWebpPlugin.SupportsFileTypes.Contains("webp"))
                    {
                        animatedWebpPlugin.SetAnimatedSource(ImageDisplay, bitmap);
                    }
                    else
                    {
                        ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);
                    }

                    animationController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF // tag GIFNULL
                }
                else
                {
                    if (uri.AbsolutePath.ToLower().EndsWith(".png"))
                    {
                        BitmapImage bitmap;
                        if (_preloadManager != null && _preloadManager.TryGet(uri.LocalPath, out var cachedPng))
                        {
                            bitmap = cachedPng;
                        }
                        else
                        {
                            var tmp = new BitmapImage();
                            tmp.BeginInit();
                            tmp.UriSource = uri;
                            tmp.CacheOption = BitmapCacheOption.OnLoad;
                            tmp.EndInit();
                            tmp.Freeze();
                            bitmap = tmp;
                        }

                        ImageDisplay.Source = bitmap;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.

                        ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);  // slow method and cannot be made async

                        animationController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF // tag GIFNULL
                    }
                    else
                    {
                        BitmapImage bitmap;
                        if (_preloadManager != null && _preloadManager.TryGet(uri.LocalPath, out var cachedImg))
                        {
                            bitmap = cachedImg;
                        }
                        else
                        {
                            var tmp = new BitmapImage();
                            tmp.BeginInit();
                            tmp.UriSource = uri;
                            tmp.CacheOption = BitmapCacheOption.OnLoad;
                            tmp.EndInit();
                            tmp.Freeze();
                            bitmap = tmp;
                        }

                        ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                        ImageDisplay.Source = bitmap;
                    }

                }

                // hide the no-image message if an image is loaded
                ImageDisplay.Visibility = Visibility.Visible;
                if (NoImageMessage != null)
                    NoImageMessage.Visibility = Visibility.Collapsed;

                if (WorkspaceLoadInProgress == false && (!openedThroughApp || Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp))
                {
                    await ResizeWindowToImage(silent: isComicMode);
                    CenterWindowOnCurrentScreen();
                }

                ApplyDisplayMode();
                await UpdateContextMenuState();

                if (!WorkspaceLoadInProgress)  // skip this for faster workspace opening. User is unlikely to benefit from preloading anyway, for immediate images in a workspace load.
                    _preloadManager?.PreloadWindow(currentImageIndex, imageFiles);
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                {
                    return;
                }

                Message($"Failed to display image: {ex.Message}");
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
                Message($"Failed to copy compressed image as file: {ex.Message}");
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

        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            using var ms = new MemoryStream();
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var temp = new System.Drawing.Bitmap(ms);
            var result = new System.Drawing.Bitmap(temp);
            return result;
        }
        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
        public List<string> supportedFileTypes = new() { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".jfif", ".webm", ".mkv", ".mp4" };
        private bool IsSupportedImageFile(string filePath)
        {
            string? extension = Path.GetExtension(filePath)?.ToLower();
            return supportedFileTypes.Contains(extension);
        }
        private bool IsSupportedImageUri(Uri uri)
        {
            string? extension = Path.GetExtension(uri.LocalPath)?.ToLower();
            return supportedFileTypes.Contains(extension);
        }
        private async void DownloadAndLoadImage(Uri uri)
        {
            try
            {
                using HttpClient client = new HttpClient();
                byte[] imageData = await client.GetByteArrayAsync(uri);
                using MemoryStream stream = new MemoryStream(imageData);

                if (!Directory.Exists(droppedInFilesPath))
                    Directory.CreateDirectory(droppedInFilesPath);

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(uri.LocalPath);
                string extension = Path.GetExtension(uri.LocalPath);
                var destinationPath = Path.Combine(droppedInFilesPath, fileNameWithoutExtension + "_" + _random.NextInt64() + extension);

                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await stream.CopyToAsync(fileStream);
                }

                await LoadImage(destinationPath, false);
            }
            catch (Exception ex)
            {
                Message($"Failed to load image from URL: {ex.Message}");
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
                    Message("Path copy failed: Image file does not exist");
                    return;
                    // could just copy bitmap in this case
                }

                CopyImageAtPathToClipboard(currentlyDisplayedImagePath);
                Message("Copied image file to clipboard.");
            }
            catch (Exception ex)
            {
                Message($"Failed to copy image to clipboard: {ex.Message}");
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
            if (recentFiles.Count > Math.Max(MaxRecentFilesInGallery, MaxRecentFilesInContextWindow))
                recentFiles.RemoveAt(recentFiles.Count - 1);

            SaveRecentFiles();
        }
        private void PrepareZoomMenu()
        {
            int[] roundZooms = { 10, 25, 50, 75, 100, 150, 200, 400, 800 };

            // Clear the existing items
            ZoomMenu.Items.Clear();

            // Populate the menu
            foreach (int zoom in roundZooms)
            {
                MenuItem zoomItem = new MenuItem
                {
                    Header = $"{zoom}%",
                    ToolTip = zoom,
                    Tag = zoom
                };
                zoomItem.Click += async (s, e) =>
                {
                    var tag = ((MenuItem)s).Tag;
                    int zoom = (int)tag;
                    double scale = (double)zoom / 100d;
                    await ZoomFromCenterToGivenScale(scale);
                };
                ZoomMenu.Items.Add(zoomItem);
            }
        }

        public async Task<System.Windows.Controls.Image?> GetImageThumbnail(string filePath, int width, int height, bool isContextWindow = false, bool useFailureThumb = false)
        {
            try  // called 10+ times every time context menu is used/updated. Could be more efficient.
            {
                if (useFailureThumb)
                {
                    filePath = Path.Combine(AppContext.BaseDirectory, "no-thumbnail.png");
                }

                using var stream = File.OpenRead(filePath);

                var bitmap = new BitmapImage();

                // The former below is faster for the context window, and the latter is more compatible with the secondary window thumbnails.
                // If we only use the latter, then it seriously slows down normal image loading since it hits slower paths. This could be improved
                // with more async re-working in the future, but for now this works well.
                if (isContextWindow)
                {
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = width;
                    bitmap.DecodePixelHeight = height;
                    bitmap.EndInit();
                    bitmap.Freeze();
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
                    bitmap.Freeze();
                }

                return new System.Windows.Controls.Image { Source = bitmap, Width = width, Height = height };
            }
            catch (Exception ex)
            {
                if (!useFailureThumb)  // TODO hacky, could clean this up. Bool protects against infinite recursion.
                {
                    return await GetImageThumbnail(filePath, width, height, isContextWindow, useFailureThumb: true);
                }

                if (!isContextWindow)  // reduce spam
                    Message($"Failed to load thumbnail: {ex.Message}");
                return new System.Windows.Controls.Image { Source = null, Width = width, Height = height };
            }
        }
        private async Task UpdateRecentFilesMenu(bool isStartUp = false)  // no side effects beyond instance. Reads from static file at this time, does not write to it.
        {
            bool recentFilesChanged = LoadRecentFiles(); // Always fetch the latest list
            if (!isStartUp && !recentFilesChanged)
                return;

            // Clear the existing items
            RecentFilesMenu.Items.Clear();

            // Populate the menu
            int added = 0;
            foreach (string file in recentFiles)
            {
                MenuItem fileItem = new MenuItem
                {
                    Header = System.IO.Path.GetFileName(file),
                    ToolTip = file,
                    Tag = file,
                    Icon = await GetImageThumbnail(file, 16, 16, true)
                };
                fileItem.Click += async (s, e) => await OpenRecentFile((string)((MenuItem)s).Tag);
                RecentFilesMenu.Items.Add(fileItem);
                added++;
                if (added >= MaxRecentFilesInContextWindow)
                    break;
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
                clearHistoryItem.Click += async (s, e) => await ClearRecentFiles();
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
                Message($"File not found: {filePath}");
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
                Message("Unable to write to recent files. Another instance may be busy.");
            }
        }
        private async void RecentFilesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            await UpdateRecentFilesMenu();
        }

        public int recentFilesHash = -1;
        // returns whether the list has been changed, based on stored hash.
        private bool LoadRecentFiles() // TODO handle exceptions: file corruption, access issues, launching with empty or missing list, manually deleting file outside of or inside of session(s).
        {
            if (recentFilesMutex.WaitOne(2000)) // Wait for up to 2 seconds
            {
                try
                {
                    if (File.Exists(recentFilesPath))
                    {
                        string json = File.ReadAllText(recentFilesPath);
                        int newHash = json.GetHashCode();
                        if (newHash == recentFilesHash)
                        {
                            return false;
                        }

                        recentFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                        recentFilesHash = newHash;
                    }
                    else
                    {
                        if (recentFilesHash == 0)
                            return false;
                        recentFiles = new List<string>();
                        recentFilesHash = 0;
                    }

                    return true;
                }
                finally  // This gets hit regardless of the returns in the try-block.
                {
                    recentFilesMutex.ReleaseMutex();
                }
            }
            else
            {
                Message("Unable to access recent files. Another instance may be busy.");
                return false;
            }
        }

        private async Task ClearRecentFiles()
        {
            recentFiles.Clear();
            SaveRecentFiles();
            await UpdateRecentFilesMenu();
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

                string currentVersion = CURRENT_VERSION;

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

        public Bitmap CreateImageForWallpaper()
        {
            var img = ImageDisplay.Source as BitmapSource;

            // Window dimensions
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;

            // Image dimensions
            double imageWidth = ImageDisplay.ActualWidth;
            double imageHeight = ImageDisplay.ActualHeight;

            // Image scale
            double? scaleX = imageScaleTransform?.ScaleX;
            double? scaleY = imageScaleTransform?.ScaleY;

            // Image translation
            double? translateX = imageTranslateTransform?.X;
            double? translateY = imageTranslateTransform?.Y;
            double? imageTrueWidth = null;
            double? imageTrueHeight = null;
            double? realScale = null;
            double? tloX = null;  // Top Left Origin, as opposed to central origin. These values are with respect to coordinates on the original image in original dimensions.
            double? tloY = null;
            double? tloWidth = null;
            double? tloHeight = null;
            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                imageTrueWidth = bitmap.PixelWidth;
                imageTrueHeight = bitmap.PixelHeight;
                realScale = imageWidth / (double)imageTrueWidth * (double)scaleX;  // ignores nuance if x and y scales don't match, i.e. stretching

                if (WindowState == WindowState.Maximized)
                {
                    var diff = GetHackBorderSizeWhenFullscreen().Left * 2;
                    windowHeight -= diff;
                    windowWidth -= diff;
                }

                // evil graphics math
                tloX = imageTrueWidth / 2 - (translateX + windowWidth / 2) / realScale;   // TODO enforce: must not be stretched (for now?) and must not have any visible margins
                tloY = imageTrueHeight / 2 - (translateY + windowHeight / 2) / realScale;
                tloWidth = windowWidth / realScale;
                tloHeight = windowHeight / realScale;
            }

            // define the crop area (X, Y, Width, Height in pixels)
            Int32Rect cropRect = new Int32Rect((int)tloX, (int)tloY, (int)tloWidth, (int)tloHeight);

            var croppedBitmap = new CroppedBitmap(img, cropRect);

            MemoryStream ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();  // supports images with alpha channels (transparency)
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
            encoder.Save(ms);
            ms.Flush();
            return new Bitmap(ms);
        }

        private void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private async Task<string> ImgBB(string path)  // docs https://api.imgbb.com/
        {
            try
            {
                using var client = new HttpClient();
                string apiKey = Cloudless.Properties.Settings.Default.ImgBBKey;

                if (string.IsNullOrEmpty(apiKey))
                {
                    Message("Add an ImgBB key in preferences to use the RIS feature.");
                    return null;
                }

                var url = $"https://api.imgbb.com/1/upload?key={apiKey}&expiration=600";  // expiration is in seconds
                var content = new MultipartFormDataContent();
                var b64 = Convert.ToBase64String(File.ReadAllBytes(path));
                content.Add(new StringContent(b64), "image");
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                var response = await client.SendAsync(request);

                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ImgBBResponse>(jsonString);

                string finalUrl = result.data.url;

                return finalUrl;
            }
            catch (Exception ex)
            {
                Message($"Error in RIS: Ensure your ImgBB key is valid");
                return null;
            }
            return null;
        }
    }

    public class ImgBBResponse
    {
        public Data? data { get; set; }
        public bool success { get; set; }
        public int status { get; set; }
    }

    public class Data
    {
        public string id { get; set; }
        public string title { get; set; }
        public string url_viewer { get; set; }
        public string url { get; set; }
        public string display_url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int size { get; set; }
        public int time { get; set; }
        public int expiration { get; set; }
        public string delete_url { get; set; }
    }

    public class GitHubRelease
    {
        public string tag_name { get; set; } = "";
        public string html_url { get; set; } = "";
        public bool prerelease { get; set; }
        public bool draft { get; set; }
        public DateTime published_at { get; set; }
    }

}
