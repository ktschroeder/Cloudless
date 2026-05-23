using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private TextBox? GetCommandTextBox()
        {
            try
            {
                if (_commandPaletteWindow?.Control != null)
                    return _commandPaletteWindow.Control.CommandTextBoxControl;
                var tb = this.FindName("CommandTextBox") as TextBox;
                return tb;
            }
            catch { return null; }
        }

        private bool IsCommandPaletteVisible()
        {
            try { return _commandPaletteWindow != null && _commandPaletteWindow.IsVisible; } catch { return false; }
        }

        private void OpenCommandPalette()
        {
            if (IsCommandPaletteVisible())
            {
                var tb = GetCommandTextBox();
                tb?.Focus();
                return;
            }

            // ensure history refreshed
            LoadCommandHistory();
            CommandHistoryIndex = CommandHistory.Count;
            TabScroll = false;

            var textBox = GetCommandTextBox();
            if (textBox != null)
            {
                textBox.Text = ":";
                textBox.CaretIndex = textBox.Text.Length;
            }

            // show floating palette if present
            if (_commandPaletteWindow != null)
            {
                _commandPaletteWindow.ShowAndFocus(textBox, this);
            }
            else
            {
                textBox?.Focus();
            }
        }

        public void CommandPalette_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Executes every time a character is added or removed.
            // Ensure there is a colon at the start.
            var tb = GetCommandTextBox();
            if (tb == null) return;
            string currentText = tb.Text;
            if (string.IsNullOrEmpty(currentText))
                tb.Text = ":";
            else if (currentText[0] != ':')
                tb.Text = $":{currentText}";
        }

        public void CommandPalette_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var tb = GetCommandTextBox();
            if (tb == null) return;
            if (tb.SelectionLength == 0 && tb.CaretIndex == 0 && tb.Text.Length > 0)
            {
                tb.CaretIndex = 1;
            }

            // TODO: make Ctrl A select all but first char. make Shift Left do nothing when at index 1.
        }

        private void CloseCommandPalette()
        {
            var win = _commandPaletteWindow;
            if (win != null)
            {
                win.Hide();
            }

            // Restore focus to the main window
            FocusManager.SetFocusedElement(this, this);
            Keyboard.Focus(this);
        }

        public async void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            if (e.Key == Key.Tab)
            {
                TabPressed(shift, control);
                e.Handled = true;
                return;
            }
            
            if (e.Key != Key.Left && e.Key != Key.Right && !shift)
                TabScroll = false;

            if (e.Key == Key.Escape)
            {
                var tb = GetCommandTextBox();
                tb.Text = ":";
                CloseCommandPalette();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var tb = GetCommandTextBox();
                string command = tb.Text.Trim();
                tb.Text = ":";
                CloseCommandPalette();  // if this is closed after execution of command, we get some sporadic weirdness with unreleased focus in a window that has been sent to a different page.
                
                if (!string.IsNullOrEmpty(command))
                {
                    await ExecuteCommand(command);
                }

                e.Handled = true;
                return;
            }
        }

        public async Task ExecuteCommand(string command)
        {
            // The user may chain together commands in one line
            List<string> individualCommands = command.Split(";").ToList();

            foreach (string c in individualCommands)
            {
                string trimmedCommand = c.Trim();
                if (!string.IsNullOrEmpty(trimmedCommand))
                {
                    bool validCommand = await ExecuteCommandInner(trimmedCommand);  // bool is currently unused
                }
            }

            // Originally it seemed better to only commit valid commands, but I find it more convenient to commit all commands, at least for now.
            // ...especially when the user makes a slight typo and would rather not fully re-type a longer command.
            CommitCommand(command);
        }

        private bool TabScroll;  // whether next tab should try to find another autocomplete candidate
        private bool TabScrollCtrl;
        private LinkedList<string> AutocompleteCandidates = new LinkedList<string>();
        private LinkedList<string> AutocompleteCandidatesCtrl = new LinkedList<string>();
        private string PreviousTabScrollText = null;
        private void TabPressed(bool shiftPressed = false, bool controlPressed = false)
        {
            var _tb_for_prev = GetCommandTextBox();
            var _tbTextPrev = _tb_for_prev?.Text ?? "";
            if (PreviousTabScrollText != _tbTextPrev)
            {
                TabScroll = false;
                TabScrollCtrl = false;
            }
                
            string foundCommandBase = null;
            string[] tabbableCommandBases = { "ws l", "ws load", "ws s", "ws save", "ws s!", "ws save!", "ws delete", "ws rename", "ws r", "ws merge", "ws m", "ws preview", "ws p" };
            foreach (string tcb in tabbableCommandBases) 
            { 
                var _tb_for_check = GetCommandTextBox();
                if (_tb_for_check != null && _tb_for_check.Text.ToLower().StartsWith($":{tcb} "))
                {
                    foundCommandBase = tcb;
                    break;
                }
            }

            if (foundCommandBase != null)
            {
                string commandBase = $":{foundCommandBase} ";
                if (!TabScroll && !controlPressed)
                {
                    var _tb_for_query = GetCommandTextBox();
                    string query = "";
                    if (_tb_for_query != null)
                        query = _tb_for_query.Text.Length == commandBase.Length ? "" : _tb_for_query.Text.Substring(commandBase.Length);
                    var wsNames = Directory.GetFiles(workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    wsNames = wsNames.Where(ws => !IsReservedWorkspaceName(ws)).ToList();  // filter out system/reserved workspace names
                    wsNames = wsNames.Where(ws => ws.ToLower().StartsWith(query.ToLower())).ToList();
                    AutocompleteCandidates.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidates.AddLast(wsName);
                    }
                    TabScroll = true;
                }
                else if (!TabScrollCtrl && controlPressed)
                {
                    var wsNames = Directory.GetFiles(workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    var recentNames = GetRecentlySavedAndLoadedWorkspaceNames();
                    wsNames = recentNames.Where(ws => wsNames.Contains(ws)).ToList();  // filter out names not present in recent history
                    wsNames = wsNames.Where(ws => !IsReservedWorkspaceName(ws)).ToList();  // filter out system/reserved workspace names
                    AutocompleteCandidatesCtrl.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidatesCtrl.AddFirst(wsName);
                    }
                    TabScrollCtrl = true;
                }
                CycleToNextAutocompleteCandidate(commandBase, reverse: shiftPressed, recency: controlPressed);  // TODO assign to list here if needed. Return result.
            }
        }

        private void CycleToNextAutocompleteCandidate(string commandBase, bool reverse = false, bool recency = false)
        {
            LinkedList<string> candidates = recency ? AutocompleteCandidatesCtrl : AutocompleteCandidates;

            if (candidates.Count == 0)
                return;

            // get next candidate
            if (!reverse)
            {
                var next = candidates.First();
                candidates.RemoveFirst();
                    var _tb_for_cycle = GetCommandTextBox();
                    if (_tb_for_cycle != null)
                        _tb_for_cycle.Text = commandBase + next;
                    if ((_tb_for_cycle?.Text ?? "").Equals(PreviousTabScrollText))  // ...then the user is "changing direction", so repeat the movement
                {
                    candidates.AddLast(next);
                    next = candidates.First();
                    candidates.RemoveFirst();
                        if (_tb_for_cycle != null)
                            _tb_for_cycle.Text = commandBase + next;
                }
                PreviousTabScrollText = _tb_for_cycle?.Text ?? PreviousTabScrollText;
                candidates.AddLast(next);
                if (_tb_for_cycle != null)
                    _tb_for_cycle.CaretIndex = _tb_for_cycle.Text.Length;
            }
            else
            {
                var next = candidates.Last();
                candidates.RemoveLast();
                var _tb_for_cycle2 = GetCommandTextBox();
                if (_tb_for_cycle2 != null)
                    _tb_for_cycle2.Text = commandBase + next;
                if ((_tb_for_cycle2?.Text ?? "").Equals(PreviousTabScrollText))  // ...then the user is "changing direction" so repeat the movement
                {
                    candidates.AddFirst(next);
                    next = candidates.Last();
                    candidates.RemoveLast();
                    if (_tb_for_cycle2 != null)
                        _tb_for_cycle2.Text = commandBase + next;
                }
                PreviousTabScrollText = _tb_for_cycle2?.Text ?? PreviousTabScrollText;
                candidates.AddFirst(next);
                if (_tb_for_cycle2 != null)
                    _tb_for_cycle2.CaretIndex = _tb_for_cycle2.Text.Length;
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

            if (command.ToLower().Equals("shutdown") || command.ToLower().Equals("sd"))  // close all instances and shutdown background process
            {
                Shutdown();
                return true;  // should be essentially unreachable
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

            if (command.ToLower().Equals("um origin"))
            {
                UnminimizeWorkspaceOriginInstances();
                return true;
            }

            if (command.ToLower().Equals("c others"))  // close all other instances
            {
                CloseAllOtherInstances();
                return true;
            }

            if (command.ToLower().Equals("c origin"))  // close all instances showing an image from this window's workspace
            {
                CloseWorkspaceOriginInstances();
                return true;
            }

            if (command.ToLower().Equals("m others"))
            {
                MinimizeAllOtherInstances();
                return true;
            }

            if (command.ToLower().Equals("m origin"))
            {
                MinimizeWorkspaceOriginInstances();
                return true;
            }

            if (command.ToLower().Equals("help"))
            {
                var window = CommandPaletteRef();
                window.Activate(); // TODO bug
                window.Focus();  // without this, focus stays on main window. If user uses 'c' hotkey, main window closes.
                                // also, this makes the window properly tabbable.

                // focus still gets taken by main window. maybe happens after here in flow? but weirdly, main window stays behind.
                // other way to see this idea: "help" and then immediately hotkey Ctrl A, vs being in main screen and doing Ctrl H then Ctrl A.

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
                if (command.ToLower().StartsWith("ws save ") && command.Length > 8)
                {
                    string name = command.Substring(8);
                    (int windowCount, int pageCount, string? error) = SaveWorkspace(name);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -2)
                        Message("A workspace by that name already exists. To overwrite it, use command 'ws save! [name]'");
                    else if (windowCount == -3)
                        Message("You're trying to save a workspace using a reserved name, which is not allowed. Did you mean to use a different command?");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows and {pageCount} pages");
                }
                else if (command.ToLower().StartsWith("ws s ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    (int windowCount, int pageCount, string? error) = SaveWorkspace(name);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -2)
                        Message("A workspace by that name already exists. To overwrite it, use command 'ws s! [name]'");
                    else if (windowCount == -3)
                        Message("You're trying to save a workspace using a reserved name, which is not allowed. Did you mean to use a different command?");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows and {pageCount} pages");
                }
                else if (command.ToLower().StartsWith("ws save! ") && command.Length > 9)
                {
                    string name = command.Substring(9);
                    (int windowCount, int pageCount, string? error) = SaveWorkspace(name, true);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -3)
                        Message("You're trying to save a workspace using a reserved name, which is not allowed. Did you mean to use a different command?");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows and {pageCount} pages");
                }
                else if (command.ToLower().StartsWith("ws s! ") && command.Length > 6)
                {
                    string name = command.Substring(6);
                    (int windowCount, int pageCount, string? error) = SaveWorkspace(name, true);
                    if (windowCount == -1)
                        Message("Failed to save workspace due to unexpected error: " + error);
                    else if (windowCount == -3)
                        Message("You're trying to save a workspace using a reserved name, which is not allowed. Did you mean to use a different command?");
                    else
                        Message($"Saved workspace {name} with {windowCount} windows and {pageCount} pages");
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
                else if (command.ToLower().Equals("ws origin"))
                {
                    RevealWorkspaceName();
                }
                else if (command.ToLower().Equals("ws origin s") || command.ToLower().Equals("ws origin s!"))
                {
                    if (imageOriginalWorkspaceName != null)
                    {
                        (int windowCount, int pageCount, string? error) = SaveWorkspace(imageOriginalWorkspaceName, true);
                        if (windowCount == -1)
                            Message("Failed to save workspace due to unexpected error: " + error);
                        else
                            Message($"Saved workspace {imageOriginalWorkspaceName} with {windowCount} windows and {pageCount} pages");
                    }
                    else
                        Message("This window does not belong to a workspace");
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
                    bool success = await PreviewWorkspace(name.Trim());
                }
                else if (command.ToLower().StartsWith("ws p ") && command.Length > 5)
                {
                    string name = command.Substring(5);
                    bool success = await PreviewWorkspace(name.Trim());
                }
                else if (command.ToLower().Equals("ws p") || command.ToLower().Equals("ws preview"))
                {
                    bool success = await PreviewWorkspace();
                }
                else if (command.ToLower().Equals("ws rev"))
                {
                    RevealDirectoryInExplorer(workspaceFilesPath);
                }
                else if (command.ToLower().Equals("ws undoload"))
                {
                    await UndoLoad();
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

                if (dims.Length != 2 || !int.TryParse(dims[0], out int width) || !int.TryParse(dims[1], out int height))
                {
                    Message("Unexpected format. Usage: dim [int] [int]");
                    return false;
                }
                    
                ResizeWindow(width, height);
                CenterWindowOnCurrentScreen();

                return true;
            }

            if (command.ToLower().Equals("cip"))  // copy image path
            {
                if (currentlyDisplayedImagePath == null)
                {
                    Message("Cannot copy image path to clipboard because no image is loaded");
                    return true;
                }

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

            if (command.ToLower().StartsWith("hotkey "))
            {
                string args = command.Substring(6);
                await SimulateHotkey(args.Trim());
                return true;
            }

            string pattern = @"^p(\d+) send$";  // e.g. "p2 send window"
            Match match = Regex.Match(command.ToLower().Trim(), pattern);
            int? matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                SendWindowToPage((int)matchInt);
                return true;
            }

            pattern = @"^p(\d+) bring$";  // e.g. "p2 bring window"
            match = Regex.Match(command.ToLower().Trim(), pattern);
            matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                SendWindowToPage((int)matchInt);
                SwapViewToPage((int)matchInt);
                return true;
            }

            pattern = @"^p(\d+) send page$";
            match = Regex.Match(command.ToLower().Trim(), pattern);
            matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                SendPageToPage((int)matchInt);
                return true;
            }

            pattern = @"^p(\d+) bring page$";
            match = Regex.Match(command.ToLower().Trim(), pattern);
            matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                SendPageToPage((int)matchInt);
                SwapViewToPage((int)matchInt);
                return true;
            }

            pattern = @"^p(\d+) clear$";
            match = Regex.Match(command.ToLower().Trim(), pattern);
            matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                ClearPage((int)matchInt);
                return true;
            }

            pattern = @"^p(\d+) swap p(\d+)$";
            match = Regex.Match(command.ToLower().Trim(), pattern);
            int? matchInt1 = match.Success ? int.Parse(match.Groups[1].Value) : null;
            int? matchInt2 = match.Success ? int.Parse(match.Groups[2].Value) : null;
            if (matchInt1.HasValue && matchInt2.HasValue)
            {
                SwapPageWithPage((int)matchInt1, (int)matchInt2);
                return true;
            }

            pattern = @"^p(\d+)$";  // e.g. "p2"
            match = Regex.Match(command.ToLower().Trim(), pattern);
            matchInt = match.Success ? int.Parse(match.Groups[1].Value) : null;
            if (matchInt.HasValue)
            {
                SwapViewToPage((int)matchInt);
                return true;
            }

            if (command.ToLower().Equals("p?"))
            {
                var pages = GetNonemptyPages();
                Message($"On page {windowPageIndex}. Non-empty pages: " + string.Join(", ", pages));

                return true;
            }

            if (command.ToLower().Equals("flatten"))
            {
                FlattenPages();

                SwapViewToPage(1);

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char ch);
        private async Task SimulateHotkey(string argsString)
        {
            string[] args = argsString.Split(" ");
            bool ctrl = args.Contains("ctrl");
            bool alt = args.Contains("alt");
            bool shift = args.Contains("shift");
            List<string> coreHotkey = args.Where(a => a != "ctrl" && a != "alt" && a != "shift").ToList();
            if (coreHotkey.Count != 1)
            {
                Message("Command failed: Expected format is exactly one character after any modifiers (ctrl alt shift)");
                return;
            }
            if (coreHotkey.First().Length != 1)
            {
                Message("Command failed: Core hotkey (ignoring modifiers) must be exactly one character");
                return;
            }
            char finalHotkeyChar = coreHotkey.First().ToCharArray().First();
            Key finalHotkey;
            try
            {
                var vkey = VkKeyScan(finalHotkeyChar);
                byte virtualKeyCode = (byte)(vkey & 0xFF);
                finalHotkey = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
            }
            catch (Exception ex)
            {
                Message("Failed to parse hotkey: " + ex.ToString());
                return;
            }

            await SimulateKeyEvent(finalHotkey, shift, ctrl, alt);
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

        public void LoadCommandHistory()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.CommandHistory;
            if (stringCollection == null)
            {
                CommandHistory = new List<string>();
            }
            else
            {
                var list = stringCollection.Cast<string>().ToList();
                CommandHistory = list;
            }
        }

        private void CommitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            LoadCommandHistory();

            // Avoid duplicate consecutive entries
            if (CommandHistory.Count == 0 || CommandHistory[^1] != command)
                CommandHistory.Add(command);

            const int HISTORY_MAX_SIZE = 100;
            StringCollection sc = new StringCollection();
            sc.AddRange(CommandHistory.TakeLast(HISTORY_MAX_SIZE).ToArray());
            Cloudless.Properties.Settings.Default.CommandHistory = sc;
            Cloudless.Properties.Settings.Default.Save();

            CommandHistoryIndex = CommandHistory.Count; // reset position
        }

        public void CommandPaletteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (CommandHistory.Count == 0)
                return;

            if (e.Key == Key.Up)
            {
                e.Handled = true;

                if (CommandHistoryIndex > 0)
                    CommandHistoryIndex--;

                var tb = GetCommandTextBox();
                if (tb != null)
                {
                    tb.Text = CommandHistory[CommandHistoryIndex];
                    tb.CaretIndex = tb.Text.Length;
                }
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;

                if (CommandHistoryIndex < CommandHistory.Count - 1)
                {
                    CommandHistoryIndex++;
                    var tb = GetCommandTextBox();
                    if (tb != null)
                        tb.Text = CommandHistory[CommandHistoryIndex];
                }
                else
                {
                    CommandHistoryIndex = CommandHistory.Count;
                    var tb = GetCommandTextBox();
                    if (tb != null)
                        tb.Text = ":";
                }

                var tbb = GetCommandTextBox();
                if (tbb != null)
                    tbb.CaretIndex = tbb.Text.Length;
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
            Message($"Set command at index {cIndex + 1} to: {command}");
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

        private List<int> UserCommandsInProgress = new List<int>();  // to prevent infinite loops if user sets command 1 to "c1 run" for example
        public async Task RunUserCommand(int cIndex)
        {
            if (UserCommandsInProgress.Contains(cIndex))
            {
                Message($"Command recursion detected and prevented at index {cIndex + 1}. This likely means you have a recursive loop in your commands (e.g. command 1 is \"c1 run\"), which would cause the program to freeze if allowed to continue. To fix this, edit your commands to remove the loop.");
                return;
            }

            UserCommandsInProgress.Add(cIndex);

            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex + 1}.");
            }
            else
            {
                await ExecuteCommand(command);
            }

            UserCommandsInProgress.Remove(cIndex);
        }

        public void LoadUserCommands()
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
