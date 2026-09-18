using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Handle tag-related commands: tag add, tag remove, tag destroy, tag list
        /// Note: Filmstrip, open, and gallery commands are now accessed via "fs tag", "open tag", "gallery tag" syntax.
        /// Returns true if the command was handled, false otherwise.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> ExecuteTagCommand(string cmd)
        {
            string lowerCmd = cmd.ToLower().Trim();

            // Normalize: support single-letter aliases for "tag", "add", "remove"
            // "t" -> "tag", "a" -> "add" (when after tag), "r" -> "remove" (when after tag)
            string processedCmd = lowerCmd;
            if (lowerCmd.StartsWith("t "))
                processedCmd = "tag " + lowerCmd.Substring(2);

            if (!processedCmd.StartsWith("tag"))
                return false;

            string args = processedCmd.Length > 3 ? processedCmd.Substring(3).Trim() : "";

            if (string.IsNullOrEmpty(args))
            {
                Message("tag: Missing action. Use 'tag add', 'tag remove', 'tag destroy', or 'tag list'.");
                return true;
            }

            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string action = parts[0].ToLower();
            string actionArgs = parts.Length > 1 ? parts[1].Trim() : "";

            // Normalize action aliases
            if (action == "a")
                action = "add";
            else if (action == "r")
                action = "remove";
            else if (action == "l")
                action = "list";

            if (action == "add")
                return HandleTagAdd(actionArgs);
            else if (action == "remove")
                return HandleTagRemove(actionArgs);
            else if (action == "destroy")
                return HandleTagDestroy(actionArgs);
            else if (action == "list")
                return HandleTagList(actionArgs);
            else if (action == "fs" || action == "open" || action == "open!" || action == "gallery")
            {
                Message($"tag: Action '{action}' is no longer supported. Use 'fs tag [query]', 'open tag [query]', 'open! tag [query]', or 'gallery tag [query]' instead.");
                return true;
            }
            else
            {
                Message($"tag: Unknown action '{action}'. Use 'add', 'remove', 'destroy', or 'list'.");
                return true;
            }
        }

        private bool HandleTagAdd(string args)
        {
            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
            {
                Message("tag add: No file is currently loaded.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(args))
            {
                Message("tag add: Missing tag name(s). Provide space-separated tag names.");
                return true;
            }

            // Parse space-separated tags
            var tags = args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToArray();

            if (tags.Length == 0)
            {
                Message("tag add: No valid tags provided.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var messages = tagManager.AddTags(currentlyDisplayedImagePath, tags);

            // Combine messages into single summary
            var addedTags = new List<string>();
            var duplicates = new List<string>();
            var reserved = new List<string>();

            foreach (var msg in messages)
            {
                if (msg.Contains("Added tag"))
                {
                    // Extract tag name from "Added tag 'xyz' to file."
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"'([^']+)'");
                    if (match.Success)
                        addedTags.Add(match.Groups[1].Value);
                }
                else if (msg.Contains("already has tag"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"'([^']+)'");
                    if (match.Success)
                        duplicates.Add(match.Groups[1].Value);
                }
                else if (msg.Contains("reserved keyword"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"'([^']+)'");
                    if (match.Success)
                        reserved.Add(match.Groups[1].Value);
                }
            }

            if (addedTags.Count > 0)
                Message($"Added tag{(addedTags.Count > 1 ? "s" : "")}: {string.Join(", ", addedTags)}");

            if (duplicates.Count > 0)
                Message($"Already has tag{(duplicates.Count > 1 ? "s" : "")}: {string.Join(", ", duplicates)}");

            if (reserved.Count > 0)
                Message($"Reserved keyword{(reserved.Count > 1 ? "s" : "")} (cannot use as tag): {string.Join(", ", reserved)}");

            return true;
        }

        private bool HandleTagRemove(string args)
        {
            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
            {
                Message("tag remove: No file is currently loaded.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(args))
            {
                Message("tag remove: Missing tag name(s). Provide space-separated tag names.");
                return true;
            }

            // Parse space-separated tags
            var tags = args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToArray();

            if (tags.Length == 0)
            {
                Message("tag remove: No valid tags provided.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var messages = tagManager.RemoveTags(currentlyDisplayedImagePath, tags);

            // Combine messages into single summary
            var removedTags = new List<string>();
            var notFound = new List<string>();

            foreach (var msg in messages)
            {
                if (msg.Contains("Removed tag"))
                {
                    // Extract tag name from "Removed tag 'xyz' from file."
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"'([^']+)'");
                    if (match.Success)
                        removedTags.Add(match.Groups[1].Value);
                }
                else if (msg.Contains("does not have tag"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(msg, @"'([^']+)'");
                    if (match.Success)
                        notFound.Add(match.Groups[1].Value);
                }
            }

            if (removedTags.Count > 0)
                Message($"Removed tag{(removedTags.Count > 1 ? "s" : "")}: {string.Join(", ", removedTags)}");

            if (notFound.Count > 0)
                Message($"File did not have tag{(notFound.Count > 1 ? "s" : "")}: {string.Join(", ", notFound)}");

            return true;
        }

        private bool HandleTagDestroy(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Message("tag destroy: Missing tag name(s). Provide space-separated tag names.");
                return true;
            }

            // Parse space-separated tags
            var tags = args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToArray();

            if (tags.Length == 0)
            {
                Message("tag destroy: No valid tags provided.");
                return true;
            }

            var tagManager = TagManager.Instance;

            foreach (var tag in tags)
            {
                var msg = tagManager.DestroyTag(tag);
                Message(msg);
            }

            return true;
        }

        private bool HandleTagList(string args)
        {
            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
            {
                Message("tag list: No file is currently loaded.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var fileTags = tagManager.GetTagsForFile(currentlyDisplayedImagePath);

            if (fileTags.Count == 0)
            {
                Message("No tags assigned to this file.");
            }
            else
            {
                Message($"Tags for this file: {string.Join(", ", fileTags)}");
            }

            return true;
        }

        private async System.Threading.Tasks.Task<bool> HandleTagFilmstrip(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Message("tag fs: Missing tag query. Use 'tag fs [tag query]'.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var matchingFiles = tagManager.QueryTags(args);

            if (matchingFiles.Count == 0)
            {
                Message($"tag fs: No files found matching query '{args}'.");
                return true;
            }

            if (_filmStripWindow == null || !_filmStripWindow.IsVisible)
            {
                ToggleFilmStrip(skipPopulation: true);
            }

            if (_filmStripWindow != null)
            {
                NonstandardPopulateFilmStrip(matchingFiles.ToArray());
                Message($"Filmstrip loaded with {matchingFiles.Count} file(s) matching query '{args}'.");
            }
            else
            {
                Message("Failed to open filmstrip window.");
                return false;
            }

            return true;
        }

        private async System.Threading.Tasks.Task<bool> HandleTagOpen(string args, int maxFiles)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Message($"tag open: Missing tag query. Use 'tag open [tag query]'.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var matchingFiles = tagManager.QueryTags(args);

            if (matchingFiles.Count == 0)
            {
                Message($"tag open: No files found matching query '{args}'.");
                return true;
            }

            var filesToOpen = matchingFiles.Take(maxFiles).ToList();

            // Filter to only files that exist on disk
            var existingFiles = filesToOpen.Where(f => System.IO.File.Exists(f)).ToList();

            if (existingFiles.Count == 0)
            {
                Message($"tag open: Query matched {matchingFiles.Count} file(s), but none exist on disk.");
                return false;
            }

            // If only one file, load in current window (same as "o" command)
            if (existingFiles.Count == 1)
            {
                await LoadImage(existingFiles[0], true);
                Message($"Opened 1 file matching query '{args}'.");
                return true;
            }

            // Multiple files: first in current window, rest in new windows (like "o [directory]")
            await LoadImage(existingFiles[0], true);

            for (int i = 1; i < existingFiles.Count; i++)
            {
                string filePath = existingFiles[i];
                try
                {
                    // Create and initialize the window on the UI thread
                    var newWindow = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var w = new MainWindow(filePath, workspaceLoad: true);
                        w.WorkspaceLoadInProgress = true;
                        return w;
                    });

                    // Load the image/video content on the UI thread
                    var loadOp = await Application.Current.Dispatcher.InvokeAsync(() => newWindow.LoadImage(filePath, true));
                    await loadOp;

                    // Show the window
                    await Application.Current.Dispatcher.InvokeAsync(() => newWindow.Show());

                    // Resize window to fit media
                    try
                    {
                        await newWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                        object content = await newWindow.Dispatcher.InvokeAsync(() => newWindow.VideoHost.Content);
                        var vplayer = content as Cloudless.PluginBase.IVideoPlayer;

                        if (vplayer != null)
                        {
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            while (sw.ElapsedMilliseconds < 2000)
                            {
                                try
                                {
                                    var dimsTask = await newWindow.Dispatcher.InvokeAsync(() =>
                                    {
                                        var vp = newWindow.VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                                        return vp?.GetDimensions();
                                    });

                                    if (dimsTask != null)
                                    {
                                        var dims = await dimsTask.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                                        if (dims != null)
                                        {
                                            await newWindow.Dispatcher.InvokeAsync(() => newWindow.ResizeWindowToImage(), System.Windows.Threading.DispatcherPriority.Render);
                                            await newWindow.Dispatcher.InvokeAsync(() => newWindow.CenterWindowOnCurrentScreen(), System.Windows.Threading.DispatcherPriority.Render);
                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"GetDimensions polling error: {ex}");
                                }

                                await Task.Delay(100).ConfigureAwait(false);
                            }
                        }

                        var resizeOp = await newWindow.Dispatcher.InvokeAsync(() => newWindow.ResizeWindowToImage(), System.Windows.Threading.DispatcherPriority.Render);
                        await resizeOp;
                        await newWindow.Dispatcher.InvokeAsync(() => newWindow.CenterWindowOnCurrentScreen(), System.Windows.Threading.DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to resize window: {ex}");
                    }
                }
                catch (Exception ex)
                {
                    Message($"Failed to load file in new window: {ex.Message}");
                }
            }

            string limitMsg = maxFiles == 10 ? $" (limited to {maxFiles})" : "";
            Message($"Opened {existingFiles.Count} file(s){limitMsg} matching query '{args}'.");
            return true;
        }

        private async System.Threading.Tasks.Task<bool> HandleTagGallery(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Message("tag gallery: Missing tag query. Use 'tag gallery [tag query]'.");
                return true;
            }

            var tagManager = TagManager.Instance;
            var matchingFiles = tagManager.QueryTags(args);

            if (matchingFiles.Count == 0)
            {
                Message($"tag gallery: No files found matching query '{args}'.");
                return true;
            }

            try
            {
                var win = new GalleryWindow(matchingFiles, title: $"Tag Gallery: {args}", workspaceName: "");
                win.Owner = this;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                win.Show();
                Message($"Gallery opened with {matchingFiles.Count} file(s) matching query '{args}'.");
                return true;
            }
            catch (Exception ex)
            {
                Message($"Failed to open gallery: {ex.Message}");
                return false;
            }
        }
    }
}
