using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Collections.Specialized;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private void OpenCommandPalette()
        {
            CommandPalette.Visibility = Visibility.Visible;
            CommandTextBox.Text = ":";
            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            _historyIndex = _commandHistory.Count;
            CommandTextBox.Focus();
        }

        private void CloseCommandPalette()
        {
            CommandPalette.Visibility = Visibility.Collapsed;

            // Restore focus to the main window
            FocusManager.SetFocusedElement(this, this);
            Keyboard.Focus(this);
        }

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseCommandPalette();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteCommand(CommandTextBox.Text.Trim());
                CloseCommandPalette();
                e.Handled = true;
            }
        }

        private void ExecuteCommand(string command)
        {
            bool validCommand = ExecuteCommandInner(command);
            // Originally it seemed better to oonly commit valid commands, but I find it more convenient to commit all commands, at least for now.
            // ...especially when the user makes a sllight typo and would rather not fully re-type a longer command.
            CommitCommand(command);
        }

        // returns whether successful (i.e. valid) command
        private bool ExecuteCommandInner(string command)
        {
            command = command.Trim();

            if (command.StartsWith(":"))
                command = command[1..];

            if (string.IsNullOrEmpty(command))
                return false;

            if (command.StartsWith("/"))
            {
                ExecuteFilenameSearch(command.Substring(1));
                return true;
            }

            if (command.ToLower().Equals("p"))  // load most recently opened image
            {
                string path = recentFiles?.FirstOrDefault();
                if (path != null)
                {
                    OpenRecentFile(path);
                }
                return true;
            }

            if (command.ToLower().Equals("c all"))  // close all instances
            {
                CloseAllOtherInstances();
                this.Close();
                return true;  // should be essentially unreachable
            }

            if (command.ToLower().Equals("c others"))  // close all other instances
            {
                CloseAllOtherInstances();
                return true;  // should be essentially unreachable
            }

            if (command.ToLower().Equals("first"))
            {
                if (imageFiles == null)
                    return true;

                JumpToIndex(0);
                return true;
            }

            if (command.ToLower().Equals("last"))
            {
                if (imageFiles == null)
                    return true;

                JumpToIndex(imageFiles.Length - 1);
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

            if (command.ToLower().StartsWith("ws "))
            {
                if (command.ToLower().StartsWith("ws save ") && command.Length > 8)  // TODO clean up this nonsense
                {
                    string name = command.Substring(8);
                    (int windowCount, string? error) = SaveWorkspace(name);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -2)
                        Message("A workstation by that name already exists. To overwrite it, use command 'ws save! [name]'");
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
                        Message("A workstation by that name already exists. To overwrite it, use command 'ws s! [name]'");
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
                    bool success = LoadWorkspace(name);
                    if (success)
                        Message("Loaded workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws l ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = LoadWorkspace(name);
                    if (success)
                        Message("Loaded workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws merge ") && command.Length > 9)
                {
                    string name = command.Substring(9);
                    bool success = LoadWorkspace(name, true);
                    if (success)
                        Message("Merged workspace: " + name);
                }
                else if (command.ToLower().StartsWith("ws m ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = LoadWorkspace(name, true);
                    if (success)
                        Message("Merged workspace: " + name);
                }
                else if (command.ToLower().Equals("ws rev"))
                {
                    RevealWorkstationDirectoryInExplorer(workspaceFilesPath);
                }
                else
                {
                    Message("Could not parse your ws command: " + command);
                    return false;
                }

                return true;
            }

            if (command.ToLower().StartsWith("c") && int.TryParse(command.Substring(1,2), out int cIndex))
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
                    RunUserCommand(cIndex-1);
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
                    LoadImage(resolvedPath, true);
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
                    LoadImage(resolvedPath, true);
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

            if (command.ToLower().Equals("cip"))  // copy image path
            {
                Clipboard.SetText(currentlyDisplayedImagePath);
                Message("Copied image path to clipboard");
                return true;
            }

            if (command.ToLower().Equals("rev"))  // reveal current image in file explorer
            {
                string path = currentlyDisplayedImagePath;
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

                JumpToIndex(clampedIndex);
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
                JumpToIndex(targetIndex);
                return true;
            }

            Message("Command not recognized");
            return false;
        }

        private void CommitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Avoid duplicate consecutive entries
            if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
                _commandHistory.Add(command);

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
        }

        private void JumpToIndex(int index)
        {
            if (index < 0 || imageFiles == null || index >= imageFiles.Count())
                return;

            currentImageIndex = index;
            DisplayImage(index, true);
        }

        private void ExecuteFilenameSearch(string query)
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
                    JumpToIndex(index);
                    return;
                }
            }

            // ...and if not found then check from the beginning of the list until the current image
            for (int index = 0; index < currentImageIndex; index++)
            {
                string fileName = Path.GetFileName(files[index]).ToLowerInvariant();

                if (fileName.Contains(lowerQuery))
                {
                    JumpToIndex(index);
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

        private void RunUserCommand(int cIndex)
        {
            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex}.");
            }
            else
            {
                ExecuteCommand(command);
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
