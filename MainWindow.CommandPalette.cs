using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Web;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private void OpenCommandPalette()
        {
            CommandPalette.Visibility = Visibility.Visible;
            CommandTextBox.Text = ":";
            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            LoadCommandHistory();
            _historyIndex = _commandHistory.Count;
            TabScroll = false;
            CommandTextBox.Focus();
        }

        private void CommandPalette_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Executes every time a character is added or removed.
            // Ensure there is a colon at the start.
            string currentText = CommandTextBox.Text;
            if (string.IsNullOrEmpty(currentText))
                CommandTextBox.Text = ":";
            else if (currentText[0] != ':')
                CommandTextBox.Text = $":{currentText}";
        }

        private void CommandPalette_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (CommandTextBox.SelectionLength == 0 && CommandTextBox.CaretIndex == 0 && CommandTextBox.Text.Length > 0)
            {
                CommandTextBox.CaretIndex = 1;
            }

            // TODO: make Ctrl A select all but first char. make Shift Left do nothing when at index 1.
        }

        private void CloseCommandPalette()
        {
            CommandPalette.Visibility = Visibility.Collapsed;

            // Restore focus to the main window
            FocusManager.SetFocusedElement(this, this);
            Keyboard.Focus(this);
        }

        private async void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            if (e.Key == Key.Tab)
            {
                TabPressed(shift);
                e.Handled = true;
                return;
            }
            
            if (e.Key != Key.Left && e.Key != Key.Right && !shift)
                TabScroll = false;

            if (e.Key == Key.Escape)
            {
                CloseCommandPalette();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                await ExecuteCommand(CommandTextBox.Text.Trim());
                CloseCommandPalette();
                e.Handled = true;
                return;
            }
        }

        private async Task ExecuteCommand(string command)
        {
            bool validCommand = await ExecuteCommandInner(command);
            // Originally it seemed better to oonly commit valid commands, but I find it more convenient to commit all commands, at least for now.
            // ...especially when the user makes a sllight typo and would rather not fully re-type a longer command.
            CommitCommand(command);
        }

        private bool TabScroll;  // whether next tab should try to find another autocomplete candidate
        private LinkedList<string> AutocompleteCandidates = new LinkedList<string>();
        private string PreviousTabScrollText = null;
        private void TabPressed(bool shiftPressed = false)
        {
            if (PreviousTabScrollText != CommandTextBox.Text)
                TabScroll = false;

            string foundCommandBase = null;
            string[] tabbableCommandBases = { "ws l", "ws load", "ws s", "ws save", "ws s!", "ws save!", "ws delete", "ws rename", "ws r", "ws merge", "ws m", "ws preview", "ws p" };
            foreach (string tcb in tabbableCommandBases) 
            { 
                if (CommandTextBox.Text.ToLower().StartsWith($":{tcb} "))
                {
                    foundCommandBase = tcb;
                    break;
                }
            }

            if (foundCommandBase != null)
            {
                string commandBase = $":{foundCommandBase} ";
                if (!TabScroll)
                {
                    string query = CommandTextBox.Text.Length == commandBase.Length ? "" : CommandTextBox.Text.Substring(commandBase.Length);
                    var wsNames = Directory.GetFiles(workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    wsNames = wsNames.Where(ws => ws.ToLower().StartsWith(query.ToLower())).ToList();
                    AutocompleteCandidates.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidates.AddLast(wsName);
                    }
                    TabScroll = true;
                }
                CycleToNextAutocompleteCandidate(commandBase, reverse: shiftPressed);
            }
        }

        private void CycleToNextAutocompleteCandidate(string commandBase, bool reverse = false)
        {
            if (AutocompleteCandidates.Count == 0)
                return;

            // get next candidate
            if (!reverse)
            {
                var next = AutocompleteCandidates.First();
                AutocompleteCandidates.RemoveFirst();
                CommandTextBox.Text = commandBase + next;  // TODO adapt this to dynamically work with other commands?
                if (CommandTextBox.Text.Equals(PreviousTabScrollText))  // ...then the user is "changing direction" so repeat the movement
                {
                    AutocompleteCandidates.AddLast(next);
                    next = AutocompleteCandidates.First();
                    AutocompleteCandidates.RemoveFirst();
                    CommandTextBox.Text = commandBase + next;
                }
                PreviousTabScrollText = CommandTextBox.Text;
                AutocompleteCandidates.AddLast(next);
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
            else
            {
                var next = AutocompleteCandidates.Last();
                AutocompleteCandidates.RemoveLast();
                CommandTextBox.Text = commandBase + next;
                if (CommandTextBox.Text.Equals(PreviousTabScrollText))  // ...then the user is "changing direction" so repeat the movement
                {
                    AutocompleteCandidates.AddFirst(next);
                    next = AutocompleteCandidates.Last();
                    AutocompleteCandidates.RemoveLast();
                    CommandTextBox.Text = commandBase + next;
                }
                PreviousTabScrollText = CommandTextBox.Text;
                AutocompleteCandidates.AddFirst(next);
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
        }

        // returns whether successful (i.e. valid) command
        private async Task<bool> ExecuteCommandInner(string command)
        {
            command = command.Trim();

            if (command.StartsWith(":"))
                command = command[1..];

            if (string.IsNullOrEmpty(command))
                return false;

            if (command.StartsWith("/"))
            {
                await ExecuteFilenameSearch(command.Substring(1));
                return true;
            }

            if (command.ToLower().Equals("p"))  // load most recently opened image
            {
                string? path = recentFiles?.FirstOrDefault();
                if (path != null)
                {
                    await OpenRecentFile(path);
                }
                return true;
            }

            if (command.ToLower().Equals("c all"))  // close all instances
            {
                CloseAllOtherInstances();
                this.Close();
                return true;  // should be essentially unreachable
            }

            if (command.ToLower().Equals("m all"))
            {
                MinimizeAllOtherInstances();
                MinimizeWindow();
                return true;
            }

            if (command.ToLower().Equals("um all"))
            {
                UnminimizeAllOtherInstances();
                return true;
            }

            if (command.ToLower().Equals("c others"))  // close all other instances
            {
                CloseAllOtherInstances();
                return true;
            }

            if (command.ToLower().Equals("m others"))
            {
                MinimizeAllOtherInstances();
                return true;
            }

            if (command.ToLower().Equals("help"))
            {
                CommandPaletteRef();
                return true;
            }

            if (command.ToLower().Equals("first"))
            {
                if (imageFiles == null)
                    return true;

                await JumpToIndex(0);
                return true;
            }

            if (command.ToLower().Equals("last"))
            {
                if (imageFiles == null)
                    return true;

                await JumpToIndex(imageFiles.Length - 1);
                return true;
            }

            if (command.ToLower().StartsWith("sort"))
            {
                if (command.ToLower().Equals("sort name asc"))
                    Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = "FileNameAscending";
                else if (command.ToLower().Equals("sort name desc"))
                    Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = "FileNameDescending";
                else if (command.ToLower().Equals("sort date asc"))
                    Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = "DateModifiedAscending";
                else if (command.ToLower().Equals("sort date desc"))
                    Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = "DateModifiedDescending";
                else
                {
                    Message("Invalid sort type");
                    return false;
                }

                Cloudless.Properties.Settings.Default.Save();
                SortImageFilesArray();
                return true;
            }

            if (command.ToLower().StartsWith("dm"))
            {
                if (command.ToLower().Equals("dm stretch") || command.ToLower().Equals("dm 1"))
                    Cloudless.Properties.Settings.Default.DisplayMode = "StretchToFit";
                else if (command.ToLower().Equals("dm zoom") || command.ToLower().Equals("dm 2"))
                    Cloudless.Properties.Settings.Default.DisplayMode = "ZoomToFill";
                else if (command.ToLower().Equals("dm best") || command.ToLower().Equals("dm 3"))
                    Cloudless.Properties.Settings.Default.DisplayMode = "BestFit";
                else if (command.ToLower().Equals("dm bestnozoom") || command.ToLower().Equals("dm 4"))
                    Cloudless.Properties.Settings.Default.DisplayMode = "BestFitWithoutZooming";
                else
                {
                    Message("Invalid display mode");
                    return false;
                }

                Cloudless.Properties.Settings.Default.Save();
                ApplyDisplayMode();
                return true;
            }

            if (command.ToLower().Equals("qs"))
            {
                Quicksave();
                return true;
            }
            if (command.ToLower().Equals("qs c"))
            {
                bool success = Quicksave();
                if (success)
                {
                    CloseAllOtherInstances();
                    this.Close();
                }
                return true;
            }
            if (command.ToLower().Equals("ql"))
            {
                await Quickload();
                return true;
            }
            if (command.ToLower().Equals("qm"))
            {
                await Quickmerge();
                return true;
            }

            if (command.ToLower().StartsWith("ws "))
            {
                if (command.ToLower().StartsWith("ws save ") && command.Length > 8)  // TODO clean up this nonsense
                {
                    string name = command.Substring(8);
                    (int windowCount, string? error) = SaveWorkspace(name);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -2)
                        Message("A workspace by that name already exists. To overwrite it, use command 'ws save! [name]'");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows");
                }
                else if (command.ToLower().StartsWith("ws s ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    (int windowCount, string? error) = SaveWorkspace(name);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -2)
                        Message("A workspace by that name already exists. To overwrite it, use command 'ws s! [name]'");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows");
                }
                else if (command.ToLower().StartsWith("ws save! ") && command.Length > 9)
                {
                    string name = command.Substring(9);
                    (int windowCount, string? error) = SaveWorkspace(name, true);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else
                        Message($"Saved workspace {name} with {windowCount} windows");
                }
                else if (command.ToLower().StartsWith("ws s! ") && command.Length > 6)
                {
                    string name = command.Substring(6);
                    (int windowCount, string? error) = SaveWorkspace(name, true);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else
                        Message($"Saved workspace {name} with {windowCount} windows");
                }
                else if (command.ToLower().StartsWith("ws load ") && command.Length > 8)
                {
                    string name = command.Substring(8);
                    bool success = await LoadWorkspace(name);
                    if (success)
                        Message("Loaded workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws l ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = await LoadWorkspace(name);
                    if (success)
                        Message("Loaded workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws delete ") && command.Length > 10)
                {
                    string name = command.Substring(10);
                    bool success = DeleteWorkspace(name);
                    if (success)
                        Message("Deleted workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws origin"))
                {
                    RevealWorkspaceName();
                }
                else if (command.ToLower().StartsWith("ws rename ") && command.Length > 10)
                {
                    string renameParams = command.Substring(10);
                    bool success = RenameWorkspace(renameParams.Split(' '));
                }
                else if (command.ToLower().StartsWith("ws r ") && command.Length > 5)
                {
                    string renameParams = command.Substring(5);
                    bool success = RenameWorkspace(renameParams.Split(' '));
                }
                else if (command.ToLower().StartsWith("ws merge ") && command.Length > 9)
                {
                    string name = command.Substring(9);
                    bool success = await LoadWorkspace(name, true);
                    if (success)
                        Message("Merged workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws m ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = await LoadWorkspace(name, true);
                    if (success)
                        Message("Merged workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws preview ") && command.Length > 9)
                {
                    string name = command.Substring(11);
                    bool success = await PreviewWorkspace(name);
                }
                else if (command.ToLower().StartsWith("ws p ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = await PreviewWorkspace(name);
                }
                else if (command.ToLower().Equals("ws rev"))
                {
                    RevealDirectoryInExplorer(workspaceFilesPath);
                }
                else
                {
                    Message("Could not parse your ws command: " + command);
                    return false;
                }

                return true;
            }

            if (command.ToLower().StartsWith("c") && command.Length > 1 && int.TryParse(command.Substring(1,2), out int cIndex))
            {
                string param = command.ToLower().Substring(3);
                if (param.StartsWith("set ") && param.Length > 4)
                {
                    SetUserCommand(cIndex-1, param.Substring(4));
                    return true;
                }
                else if (param.Equals("view"))
                {
                    ViewUserCommand(cIndex-1);
                    return true;
                }
                else if (param.Equals("run"))
                {
                    await RunUserCommand(cIndex-1);
                    return true;
                }
            }

            if (command.ToLower().StartsWith("o "))
            {
                // open image at relative or absolute path. "o C:\images\foo.png". "o ../otherfolder"
                string relativeOrAbsolutePath = command.Substring(2);
                string resolvedPath = relativeOrAbsolutePath;
                if (currentDirectory != null)
                    resolvedPath = Path.GetFullPath(relativeOrAbsolutePath, currentDirectory);

                if (Directory.Exists(resolvedPath))
                {
                    LoadImagesInDirectory(resolvedPath);
                }
                else if (File.Exists(resolvedPath)) 
                {
                    await LoadImage(resolvedPath, true);
                }
                return true;
            }
            if (command.ToLower().StartsWith("o! "))
            {
                // open image at relative or absolute path. "o C:\images\foo.png". "o ../otherfolder"
                string relativeOrAbsolutePath = command.Substring(3);
                string resolvedPath = relativeOrAbsolutePath;
                if (currentDirectory != null)
                    resolvedPath = Path.GetFullPath(relativeOrAbsolutePath, currentDirectory);

                if (Directory.Exists(resolvedPath))
                {
                    LoadImagesInDirectory(resolvedPath, true);
                }
                else if (File.Exists(resolvedPath))
                {
                    await LoadImage(resolvedPath, true);
                }
                return true;
            }

            if (command.ToLower().StartsWith("rec "))
            {
                if (int.TryParse(command.Substring(4), out int count) && count > 0)
                {
                    var paths = recentFiles?.Take(count) ?? new List<string>();
                    foreach (var path in paths)
                    {
                        var newWindow = new MainWindow(path);
                        newWindow.Show();
                        //OpenRecentFile(path);
                    }
                    return true;
                }

                return false;
            }

            if (command.ToLower().StartsWith("dim "))
            {
                if (command.Length < 5)
                    return false;

                string dim = command.Substring(4);
                var dims = dim.Split(' ');
                if (dims.Length != 2)
                    return false;

                if (!int.TryParse(dims[0], out int width) || !int.TryParse(dims[1], out int height))
                    return false;

                ResizeWindow(width, height);
                CenterWindowOnCurrentScreen();

                return true;
            }

            if (command.ToLower().Equals("cip"))  // copy image path
            {
                Clipboard.SetText(currentlyDisplayedImagePath);
                Message("Copied image path to clipboard");
                return true;
            }

            if (command.ToLower().Trim().Equals("ris"))  // reverse image search
            {
                await ReverseImageSearch(currentlyDisplayedImagePath, "g");
                return true;
            }

            if (command.ToLower().StartsWith("ris ") && command.Length > 4)  // reverse image search
            {
                await ReverseImageSearch(currentlyDisplayedImagePath, command.Split(' ')[1].Trim().ToLower());
                return true;
            }

            if (command.ToLower().Equals("rev"))  // reveal current image in file explorer
            {
                string? path = currentlyDisplayedImagePath;
                if (path == null)
                {
                    Message("Cannot reveal image in Explorer because no image is loaded");
                }
                else
                {
                    RevealImageInExplorer(path);
                }
                return true;
            }

            int targetIndex;
            if (command.StartsWith("+") || command.StartsWith("-"))
            {
                if (imageFiles == null)
                    return true;

                // Relative jump
                if (int.TryParse(command, out int offset))
                {
                    targetIndex = currentImageIndex + offset;
                }
                else
                {
                    Message("Invalid relative index");
                    return true;
                }

                // Clamp to valid range
                var clampedIndex = Math.Max(0, Math.Min(targetIndex, imageFiles.Count() - 1));
                if (clampedIndex != targetIndex)
                {
                    if (clampedIndex == 0)
                        Message("Relative jump was clamped to first image in directory");
                    else
                        Message("Relative jump was clamped to final image in directory");
                }

                await JumpToIndex(clampedIndex);
                return true;
            }
            else if (int.TryParse(command, out targetIndex))  // Absolute jump
            {
                if (imageFiles == null)
                    return true;

                // Convert from 1-based user input to 0-based internal index
                targetIndex -= 1;

                if (targetIndex == -1)  // user input "0"
                {
                    Message("You are in 1-indexing mode, so the first image has index 1, not 0.");
                }

                // Clamp to valid range
                targetIndex = Math.Max(0, Math.Min(targetIndex, imageFiles.Count() - 1));
                await JumpToIndex(targetIndex);
                return true;
            }

            Message("Command not recognized");
            return false;
        }

        private async Task ReverseImageSearch(string filePath, string serviceArg) {
            var validServiceArgs = new List<string>() { "google", "g", "bing", "b", "yandex", "y", "tineye", "t", "saucenao", "s" };
            if (!validServiceArgs.Contains(serviceArg))
            {
                Message("Invalid argument specifying reverse image search service.");
                return;
            }

            ShowLoadingOverlay($"Uploading file for reverse image search...");  // takes a couple seconds or so
            var hostedImageUrl = await ImgBB(filePath);
            HideLoadingOverlay();
            if (hostedImageUrl == null)
                return;

            string Encode(string url) => HttpUtility.UrlEncode(url);
            string finalUrl = null;

            switch (serviceArg)
            {
                case "google" or "g":
                    finalUrl = $"https://www.google.com/searchbyimage?image_url={Encode(hostedImageUrl)}";
                    break;
                case "bing" or "b":
                    finalUrl = $"https://www.bing.com/images/search?view=detailv2&iss=sbi&imgurl={Encode(hostedImageUrl)}";
                    break;
                case "yandex" or "y":
                    finalUrl = $"https://yandex.com/images/search?rpt=imageview&url={Encode(hostedImageUrl)}";
                    break;
                case "tineye" or "t":
                    finalUrl = $"https://tineye.com/search?url={Encode(hostedImageUrl)}";
                    break;
                case "saucenao" or "s":
                    finalUrl = $"https://saucenao.com/search.php?url={Encode(hostedImageUrl)}";
                    break;
            }

            OpenUrl(finalUrl ?? $"https://www.google.com/searchbyimage?image_url={Encode(hostedImageUrl)}");
        }

        private void LoadCommandHistory()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.CommandHistory;
            if (stringCollection == null)
            {
                _commandHistory = new List<string>();
            }
            else
            {
                var list = stringCollection.Cast<string>().ToList();
                _commandHistory = list;
            }
        }

        private void CommitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            LoadCommandHistory();

            // Avoid duplicate consecutive entries
            if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
                _commandHistory.Add(command);

            const int HISTORY_MAX_SIZE = 100;
            StringCollection sc = new StringCollection();
            sc.AddRange(_commandHistory.TakeLast(HISTORY_MAX_SIZE).ToArray());
            Cloudless.Properties.Settings.Default.CommandHistory = sc;
            Cloudless.Properties.Settings.Default.Save();

            _historyIndex = _commandHistory.Count; // reset position
        }

        private void CommandPaletteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_commandHistory.Count == 0)
                return;

            if (e.Key == Key.Up)
            {
                e.Handled = true;

                if (_historyIndex > 0)
                    _historyIndex--;

                CommandTextBox.Text = _commandHistory[_historyIndex];
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;

                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    CommandTextBox.Text = _commandHistory[_historyIndex];
                }
                else
                {
                    _historyIndex = _commandHistory.Count;
                    CommandTextBox.Text = ":";
                }

                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }

            //// TODO revisit
            //var textBox = sender as TextBox;
            //if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            //{
            //    if (textBox != null && textBox.Text.Length > 1)
            //    {
            //        textBox.Select(1, textBox.Text.Length - 1);

            //        // mark event handled to prevent default Ctrl+A
            //        e.Handled = true;
            //    }
            //}
            //if (textBox != null && e.Key == Key.Left &&
            //    (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            //{
            //    // If cursor is at index 1 (between 1st and 2nd char)
            //    if (textBox.CaretIndex == 1)
            //    {
            //        // Stop the default Shift+Left selection behavior
            //        e.Handled = true;
            //    }
            //}
        }

        private async Task JumpToIndex(int index)
        {
            if (index < 0 || imageFiles == null || index >= imageFiles.Count())
                return;

            currentImageIndex = index;
            await DisplayImage(index, true);
        }

        private async Task ExecuteFilenameSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Message("Search string is empty");
                return;
            }

            var files = imageFiles ?? Array.Empty<string>();
            int count = files.Count();

            if (count == 0)
                return;

            string lowerQuery = query.ToLowerInvariant();

            // Start searching *after* the current image
            for (int offset = 1; offset < count - currentImageIndex; offset++)
            {
                int index = currentImageIndex + offset;
                string fileName = Path.GetFileName(files[index]).ToLowerInvariant();

                if (fileName.Contains(lowerQuery))
                {
                    await JumpToIndex(index);
                    return;
                }
            }

            // ...and if not found then check from the beginning of the list until the current image
            for (int index = 0; index < currentImageIndex; index++)
            {
                string fileName = Path.GetFileName(files[index]).ToLowerInvariant();

                if (fileName.Contains(lowerQuery))
                {
                    await JumpToIndex(index);
                    Message("Continuing search from start of directory");
                    return;
                }
            }

            Message($"No match for \"{query}\"");
        }

        private void SetUserCommand(int cIndex, string command)
        {
            LoadUserCommands();
            UserCommands[cIndex] = command;
            SaveUserCommands();
        }

        private void ViewUserCommand(int cIndex)
        {
            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex}.");
            }
            else
            {
                Message($"{command}");
            }
        }

        private async Task RunUserCommand(int cIndex)
        {
            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex}.");
            }
            else
            {
                await ExecuteCommand(command);
            }
        }

        private void LoadUserCommands()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.UserCommands;
            if (stringCollection == null)
            {
                UserCommands = new List<string>() {"","","","","","","",""};
            }
            else
            {
                var list = stringCollection.Cast<string>().ToList();
                UserCommands = list;
            }
        }

        private void SaveUserCommands()
        {
            StringCollection stringCollection = [.. UserCommands.ToArray()];
            Cloudless.Properties.Settings.Default.UserCommands = stringCollection;
            Cloudless.Properties.Settings.Default.Save();
        }
    }
}
