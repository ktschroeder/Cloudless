using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Collections.Specialized;
using Cloudless.Properties;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        public string GetVersion()
        {
            return CURRENT_VERSION;
        }

        public CloudlessWindowState GetWindowState(Dictionary<IntPtr, int> zOrderMap)
        {
            if (imageScaleTransform == null || imageTranslateTransform == null) throw new NullReferenceException();

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int zOrder = zOrderMap.TryGetValue(hwnd, out int z) ? z : int.MaxValue;

            var renderWidth = Double.IsNaN(ImageDisplay.Width) ? ImageDisplay.ActualWidth : ImageDisplay.Width;
            var renderHeight = Double.IsNaN(ImageDisplay.Height) ? ImageDisplay.ActualHeight : ImageDisplay.Height;

            var state = new CloudlessWindowState()
            {
                ImagePath = currentlyDisplayedImagePath ?? "",
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                CloudlessAppVersion = GetVersion(),
                IsMaximized = this.WindowState == WindowState.Maximized,
                IsMinimized = this.WindowState == WindowState.Minimized,
                DisplayMode = Cloudless.Properties.Settings.Default.DisplayMode,
                Zoom = imageScaleTransform.ScaleX,  // Y would be the same as X regardless
                PanX = imageTranslateTransform.X,
                PanY = imageTranslateTransform.Y,
                ZOrder = zOrder,
                RenderWidth = renderWidth,
                RenderHeight = renderHeight,
                PageIndex = windowPageIndex,
                WindowWasMaximizedPriorToHidingForPage = windowWasMaximizedPriorToHidingForPage,
                WindowWasMinimizedPriorToHidingForPage = windowWasMinimizedPriorToHidingForPage
            };

            // Persist video loop start/end if set for this window
            try
            {
                if (this._videoLoopStart.HasValue)
                    state.LoopStartMs = this._videoLoopStart.Value.TotalMilliseconds;
                if (this._videoLoopEnd.HasValue)
                    state.LoopEndMs = this._videoLoopEnd.Value.TotalMilliseconds;
            }
            catch { }

            return state;
        }

        // returns (window count saved, distinct page count, error message if applicable). Former int can be negative for errors.
        public (int, int, string?) SaveWorkspace(string workspaceName = "MainWorkspace", bool allowOverwrite = false, bool isSystem = false)
        {
            try
            {
                if (!isSystem && IsReservedWorkspaceName(workspaceName))
                {
                    return (-3, 0, null);  // TODO should clean up this error reporting
                }

                if (!Directory.Exists(workspaceFilesPath))
                    Directory.CreateDirectory(workspaceFilesPath);

                UpdateRecentlySavedAndLoadedWorkspaceNames(workspaceName);

                var workspace = new CloudlessWorkspace();
                string workspaceFilePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");

                if (!allowOverwrite && File.Exists(workspaceFilePath))
                {
                    return (-2, 0, null);
                }

                var zOrderMap = GetZOrderForCurrentProcessWindows();  // TODO unused? probably replace a few lines below.

                foreach (var window in Application.Current.Windows.OfType<MainWindow>())
                {
                    CloudlessWindowState cws;
                    if (window.WindowState == WindowState.Minimized)
                    {  // "WindowState" on this line refers to Windows's window state (maximied, e.g.), not CloudlessWindowState
                        cws = window.stateUponMinimizing;
                        cws.IsMinimized = true;
                    }
                    else
                        cws = window.GetWindowState(GetZOrderForCurrentProcessWindows());

                    workspace.CloudlessWindows.Add(cws);
                }

                workspace.CurrentPageIndex = GetCurrentPageIndex();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(workspace, options);
                File.WriteAllText(workspaceFilePath, json);

                int distinctPages = workspace.CloudlessWindows.Select(w => w.PageIndex).Distinct().Count();

                return (workspace.CloudlessWindows.Count, distinctPages, null);
            }
            catch (Exception e)
            {
                return (-1, 0, e.Message);
            }
        }

        private void Shutdown()
        {
            CloseAllOtherInstances();

            var app = Application.Current;
            // Trigger normal shutdown (OnExit will run and cleanup)
            app.Shutdown();
        }

        private void CloseAllOtherInstances()
        {
            var windowsToClose = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToClose)
            {
                w.Close();
            }
        }

        private void CloseWorkspaceOriginInstances()
        {
            foreach (var window in Application.Current.Windows.OfType<MainWindow>())
            {
                if (window.imageOriginalWorkspaceName != null && window.imageOriginalWorkspaceName.Equals(this.imageOriginalWorkspaceName) && window != this)
                    window.Close();

            }
            Close();
        }


        private void MinimizeAllOtherInstances()
        {
            var windowsToMinimize = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToMinimize)
            {
                w.WindowState = WindowState.Minimized;
            }
        }

        private void MinimizeWorkspaceOriginInstances()
        {
            foreach (var window in Application.Current.Windows.OfType<MainWindow>())
            {
                if (window.imageOriginalWorkspaceName != null && window.imageOriginalWorkspaceName.Equals(this.imageOriginalWorkspaceName))
                    window.WindowState = WindowState.Minimized;
            }
        }

        private void Deflash()
        {
            int APPROACH = 2;
            foreach (var window in Application.Current.Windows.OfType<MainWindow>())
            {
                if (window.WindowState != WindowState.Minimized)  // window != this && 
                {
                    if (APPROACH == 1)
                    {
                        WindowState prevState = window.WindowState;
                        window.WindowState = WindowState.Minimized;
                        window.WindowState = prevState;
                    }
                    else if (APPROACH == 2)
                    {
                        window.Hide();
                        window.Show();
                    }
                    else if (APPROACH == 3)
                    {
                        window.Hide();
                        window.ShowInTaskbar = false;
                        window.ShowInTaskbar = true;
                        window.Show();
                    }
                    //window.ShowInTaskbar = false;
                    //window.ShowInTaskbar = true;
                }
            }
        }

        private void UnminimizeAllOtherInstances()
        {
            var windowsToUnminimize = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToUnminimize)
            {
                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;
            }
        }

        private void UnminimizeWorkspaceOriginInstances()
        {
            foreach (var window in Application.Current.Windows.OfType<MainWindow>())
            {
                if (window.imageOriginalWorkspaceName != null && window.imageOriginalWorkspaceName.Equals(this.imageOriginalWorkspaceName) && window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
            }
        }

        public async Task<bool> PreviewWorkspace(string workspaceName = "")
        {
            // TODO adjust gallery window to account for isMinimized: add a graphic or something. Maybe reduced opacity thumb with a mini banner label.

            if (string.IsNullOrEmpty(workspaceName))
            {
                var win = new GalleryWindow(new List<string>(), title: "Workspace Preview: (none selected)", workspaceName: workspaceName);
                win.Owner = this;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                win.Show();

                return true;
            }

            try
            {
                string workspaceFilePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");
                // TODO I think this fails if user has the file open for reading in a text editor?
                string json = File.ReadAllText(workspaceFilePath);

                var workspace = JsonSerializer.Deserialize<CloudlessWorkspace>(json);

                if (workspace == null)
                {
                    Message("Invalid workspace file.");
                    return false;
                }

                var wsFiles = workspace.CloudlessWindows.Select(cw => cw.ImagePath);

                var win = new GalleryWindow(wsFiles, title: "Workspace Preview: " + workspaceName, workspaceName: workspaceName);
                win.Owner = this;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                win.Show();

                return true;
            }
            catch (FileNotFoundException)
            {
                Message("Workspace file not found.");
                return false;
            }
            catch (Exception)
            {
                Message("Unexpected error previewing workspace.");
                return false;
            }
        }

        public void RevealWorkspaceName()
        {
            if (imageOriginalWorkspaceName == null)
            {
                Message("This image did not come directly from loading a workspace.");
            }
            else
            {
                Message("Workspace: " + imageOriginalWorkspaceName);
            }
        }

        public bool RenameWorkspace(string[] workspaceNames)  // TODO should update "recent workspaces" logic to be smart about this renaming
        {
            try
            {
                if (workspaceNames.Length != 2)
                {
                    Message("Unexpected parameters. Use: ws rename [workspace name to replace] [new workspace name]");
                    return false;
                }

                string from = workspaceNames[0].Trim();
                string to = workspaceNames[1].Trim();

                string oldWorkspaceFilePath = Path.Combine(workspaceFilesPath, from + ".cloudless");
                string newWorkspaceFilePath = Path.Combine(workspaceFilesPath, to + ".cloudless");

                if (!File.Exists(oldWorkspaceFilePath))
                {
                    throw new FileNotFoundException();
                }
                if (File.Exists(newWorkspaceFilePath))
                {
                    Message("Failed to rename: there is already a workspace named " + to);
                    return false;
                }

                File.Move(oldWorkspaceFilePath, newWorkspaceFilePath);
                return true;
            }
            catch (FileNotFoundException)
            {
                Message("Workspace file not found.");
                return false;
            }
            catch (Exception)
            {
                Message("Unexpected error renaming workspace.");
                return false;
            }
        }

        public bool DeleteWorkspace(string workspaceName)
        {
            try
            {
                string workspaceFilePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");

                if (!File.Exists(workspaceFilePath))
                {
                    throw new FileNotFoundException();
                }

                File.Delete(workspaceFilePath);
                return true;
            }
            catch (FileNotFoundException)
            {
                Message("Workspace file not found.");
                return false;
            }
            catch (Exception)
            {
                Message("Unexpected error deleting workspace.");
                return false;
            }
        }

        // returns whether successful
        public async Task<bool> LoadWorkspace(string workspaceName = "MainWorkspace", bool merge = false)
        {
            // if loading undoload, we want to save to undoload_slot, load undoload, then delete undoload and replace it with renamed undoload_slot.
            try
            {
                SaveWorkspace(workspaceName.Equals(UNDOLOAD_NAME) ? UNDOLOAD_SLOT_NAME : UNDOLOAD_NAME, allowOverwrite: true, isSystem: true);
            }
            catch (Exception e)
            {
                Message("Failed to save system undo file during loading");
            }

            try
            {
                UpdateRecentlySavedAndLoadedWorkspaceNames(workspaceName);

                string workspaceFilePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");
                // TODO I think this fails if user has the file open for reading in a text editor?
                string json = File.ReadAllText(workspaceFilePath);

                var workspace = JsonSerializer.Deserialize<CloudlessWorkspace>(json);
                workspace.WorkspaceName = workspaceName;

                if (workspace == null)
                {
                    Message("Invalid workspace file.");
                    return false;
                }

                if (!merge)
                {
                    //Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    this.CloseAllOtherInstances();
                    //Thread.Sleep(150); // brief grace period (optional but helps UX)
                    MinimizeWindow();
                    await CreateWindowsForWorkspace(workspace);
                    this.Close();
                }
                else
                {
                    await CreateWindowsForWorkspace(workspace);
                }

                return true;
            }
            catch (FileNotFoundException ex)
            {
                Message("Workspace file not found.");
                return false;
            }
            catch (Exception ex)
            {
                Message("Unexpected error loading workspace.");
                return false;
            }
        }

        private async Task<MainWindow> CreateWindowForWorkspace(CloudlessWindowState state, string workspaceName, int currentPageIndex)
        {
            var window = new MainWindow(state.ImagePath, state.Width, state.Height, workspaceLoad: true);
            window.WorkspaceLoadInProgress = true;
            window.WorkspaceLoadZOrder = state.ZOrder;
            await window.LoadImage(state.ImagePath, false);
            await window.ApplyWindowState(state);

            return window;
        }

        public async Task CreateWindowsForWorkspace(CloudlessWorkspace workspace)
        {
            var zOrderedWindows = workspace.CloudlessWindows.OrderByDescending(w => w.ZOrder).ToList();
            List<(MainWindow, CloudlessWindowState)> createdWindowsWithStates = new List<(MainWindow, CloudlessWindowState)>();
            
            foreach (var state in zOrderedWindows)
            {
                var window = await CreateWindowForWorkspace(state, workspace.WorkspaceName, workspace.CurrentPageIndex);
                createdWindowsWithStates.Add((window, state));
            }

            // Show all windows on UI thread
            foreach (var (window, state) in createdWindowsWithStates.OrderByDescending(w => w.Item1.WorkspaceLoadZOrder))
            {
                window.Show();
            }
            
            // Activate in reverse z-order (highest z-order last, so it ends up on top)
            foreach (var (window, state) in createdWindowsWithStates.OrderByDescending(w => w.Item1.WorkspaceLoadZOrder))
            {
                window.Activate();
            }
            
            // Defer expensive operations to background without blocking
            _ = Task.Run(async () =>
            {
                foreach (var (window, state) in createdWindowsWithStates.OrderByDescending(w => w.Item1.WorkspaceLoadZOrder))
                {
                    await window.PostProcessLoadedWindowDeferred(state, workspace.WorkspaceName, workspace.CurrentPageIndex);
                }
            });

            if (workspace.CurrentPageIndex != GetCurrentPageIndex())
                SwapViewToPage(workspace.CurrentPageIndex);

            ThemeManager.ApplyTheme(Cloudless.Properties.Settings.Default["Theme"] as string);

            List<string> paths = zOrderedWindows.Select(w => w.ImagePath).Where(p => !string.IsNullOrEmpty(p)).ToList();
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    AddToRecentFiles(path, save: false);
                }
            }
            SaveRecentFiles();
        }

        public async Task ApplyWindowState(CloudlessWindowState state)
        {
            WorkspaceLoadInProgress = true;

            //Cloudless.Properties.Settings.Default.DisplayMode = state.DisplayMode;
            //ApplyDisplayMode();
            // This gets weird and complicated since all instances share the same config file. It might make sense to just default all windows to a best fit mode.
            // Anyway, it's hard to imagine a use case where someone would really want a workspace with mixed modes.

            ResizeWindow(state.Width, state.Height);
            RepositionWindow(state.Left, state.Top);
            if (state.IsMaximized)
                await ToggleFullscreen();

            if (state.DisplayMode.ToLower().StartsWith("best"))  // best fit or zoomless best fit
            {
                if (imageScaleTransform == null || imageTranslateTransform == null) throw new NullReferenceException();

                // This essentially applies cropping
                await ToggleCropMode(true, true);
                ImageDisplay.Width = state.RenderWidth;
                ImageDisplay.Height = state.RenderHeight;

                imageScaleTransform.ScaleX = state.Zoom;
                imageScaleTransform.ScaleY = state.Zoom;
                imageTranslateTransform.X = state.PanX;
                imageTranslateTransform.Y = state.PanY;

                //ToggleCropMode(false, true);  // toggling this here is too early and causes the image to be resized undesirably.
            }
        }

        public async Task PostProcessLoadedWindow(CloudlessWindowState state, string? workspaceName = null, int startingPageIndex = 1, bool isDuplicating = false)
        {
            Show();

            if (state.PageIndex == startingPageIndex)
            {
                if (state.IsMinimized)
                    MinimizeWindow(state);  // this must be done after calling Show() on window, or else image is re-rendered improperly later
            }
            else
            {
                if (state.WindowWasMaximizedPriorToHidingForPage)
                    WindowState = WindowState.Maximized;
                if (state.WindowWasMinimizedPriorToHidingForPage)
                    MinimizeWindow(state);
            }

            if (!isDuplicating)
                SendWindowToPage(state.PageIndex);

            await ToggleCropMode(setTo: false, silent: true);

            imageOriginalWorkspaceName = workspaceName;
            // Apply any saved video loop range for this window
            try
            {
                if ((state.LoopStartMs.HasValue || state.LoopEndMs.HasValue) && VideoHost.Content is Cloudless.PluginBase.IVideoPlayer vp)
                {
                    TimeSpan? s = state.LoopStartMs.HasValue ? TimeSpan.FromMilliseconds(state.LoopStartMs.Value) : null;
                    TimeSpan? e = state.LoopEndMs.HasValue ? TimeSpan.FromMilliseconds(state.LoopEndMs.Value) : null;
                    vp.SetLoopRange(s, e);
                    // update host-side tracking fields so UI/commands reflect the loaded state
                    try { this._videoLoopStart = s; } catch { }
                    try { this._videoLoopEnd = e; } catch { }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: plugin may not support loop ranges or may not be ready yet
                Console.WriteLine($"Failed to apply saved loop range: {ex.Message}");
            }

            ShowInTaskbar = false;  // this toggle prevents a bunch of annoying flashes for each new window in taskbar when opening a workstation from File Explrorer
            Activate();
            ShowInTaskbar = true;

            WorkspaceLoadInProgress = false;
        }

        /// <summary>
        /// Post-processing that is deferred to run asynchronously after windows are shown.
        /// All UI operations must be dispatched back to the UI thread.
        /// </summary>
        public async Task PostProcessLoadedWindowDeferred(CloudlessWindowState state, string? workspaceName = null, int startingPageIndex = 1, bool isDuplicating = false)
        {
            // All WPF operations must be marshaled back to the UI thread via Dispatcher
            await Dispatcher.InvokeAsync(async () =>
            {
                if (state.PageIndex == startingPageIndex)
                {
                    if (state.IsMinimized)
                        MinimizeWindow(state);
                }
                else
                {
                    if (state.WindowWasMaximizedPriorToHidingForPage)
                        WindowState = WindowState.Maximized;
                    if (state.WindowWasMinimizedPriorToHidingForPage)
                        MinimizeWindow(state);
                }

                if (!isDuplicating)
                    SendWindowToPage(state.PageIndex);

                await ToggleCropMode(setTo: false, silent: true);

                imageOriginalWorkspaceName = workspaceName;
                // Apply any saved video loop range for this window
                try
                {
                    if ((state.LoopStartMs.HasValue || state.LoopEndMs.HasValue) && VideoHost.Content is Cloudless.PluginBase.IVideoPlayer vp)
                    {
                        TimeSpan? s = state.LoopStartMs.HasValue ? TimeSpan.FromMilliseconds(state.LoopStartMs.Value) : null;
                        TimeSpan? e = state.LoopEndMs.HasValue ? TimeSpan.FromMilliseconds(state.LoopEndMs.Value) : null;
                        vp.SetLoopRange(s, e);
                        try { this._videoLoopStart = s; } catch { }
                        try { this._videoLoopEnd = e; } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to apply saved loop range: {ex.Message}");
                }

                ShowInTaskbar = false;
                ShowInTaskbar = true;

                WorkspaceLoadInProgress = false;
            });
        }

        static Dictionary<IntPtr, int> GetZOrderForCurrentProcessWindows()
        {
            var result = new Dictionary<IntPtr, int>();
            uint currentPid = (uint)Process.GetCurrentProcess().Id;

            int z = 0;
            IntPtr hwnd = NativeMethods.GetTopWindow(IntPtr.Zero);

            while (hwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);

                if (pid == currentPid)
                {
                    result[hwnd] = z++;
                }

                hwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDNEXT);
            }

            return result;
        }

        const string QUICKSAVE_NAME = "_system_quicksave";
        const string UNDOLOAD_NAME = "_system_undoload";
        const string UNDOLOAD_SLOT_NAME = "_system_undoload_slot";
        private bool Quicksave()  // returns whether successful
        {
            (int windowCount, int pageCount, string? error) = SaveWorkspace(QUICKSAVE_NAME, allowOverwrite: true, isSystem: true);
            if (windowCount == -1)
            {
                Message("Failed to quicksave due to unexpected error: " + error);
                return false;
            }
            else
            {
                Message($"Quicksave done with {windowCount} windows and {pageCount} pages");
                return true;
            }
        }
        private async Task Quickload()
        {
            await LoadWorkspace(QUICKSAVE_NAME);
        }
        private async Task Quickmerge()
        {
            await LoadWorkspace(QUICKSAVE_NAME, true);
        }
        private async Task UndoLoad()
        {
            await LoadWorkspace(UNDOLOAD_NAME);
            try
            {
                string path = Path.Combine(workspaceFilesPath, UNDOLOAD_NAME + ".cloudless");
                File.Delete(path);
                string slotPath = Path.Combine(workspaceFilesPath, UNDOLOAD_SLOT_NAME + ".cloudless");
                File.Move(slotPath, path);
            }
            catch (Exception e)
            {
                Message("Error while managing system undoload files: " + e.Message);
            }
        }

        public static List<string> GetRecentlySavedAndLoadedWorkspaceNames()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.RecentWorkspaces;
            return stringCollection?.Cast<string>().ToList() ?? new List<string>();
        }

        public void UpdateRecentlySavedAndLoadedWorkspaceNames(string workspaceName)
        {
            if (string.IsNullOrWhiteSpace(workspaceName))
                return;

            var current = GetRecentlySavedAndLoadedWorkspaceNames();
            while (current.Contains(workspaceName))
                current.Remove(workspaceName);

            current.Add(workspaceName);

            const int HISTORY_MAX_SIZE = 100;
            StringCollection sc = new StringCollection();
            sc.AddRange(current.TakeLast(HISTORY_MAX_SIZE).ToArray());
            Cloudless.Properties.Settings.Default.RecentWorkspaces = sc;
            Cloudless.Properties.Settings.Default.Save();
        }

        public static bool IsReservedWorkspaceName(string name)
        {
            name = name.ToLower().Trim();
            List<string> reservedNames = new List<string>()
            {
                "origin", "undoload", "quicksave"
            };

            if (reservedNames.Contains(name))
                return true;

            if (name.StartsWith("_system_"))
                return true;

            return false;
        }

        public int GetCurrentPageIndex()
        {
            if (GlobalStartup)
            {
                SetCurrentPageIndex(1);
            }

            int index = Settings.Default.CurrentPage;
            return index;
        }

        // returns whether successful
        public bool SetCurrentPageIndex(int index)
        {
            if (index < 1 || index > 8)
            {
                Message($"Invalid page index: {index}. Must be from 1 to 8.");
                return false;
            }

            Settings.Default.CurrentPage = index;
            Settings.Default.Save();
            return true;
        }

        private bool windowWasMinimizedPriorToHidingForPage = false;
        private bool windowWasMaximizedPriorToHidingForPage = false;

        public void HideWindowForPages()
        {
            windowWasMinimizedPriorToHidingForPage = this.WindowState == WindowState.Minimized || windowWasMinimizedPriorToHidingForPage;
            windowWasMaximizedPriorToHidingForPage = this.WindowState == WindowState.Maximized || windowWasMaximizedPriorToHidingForPage;

            MinimizeWindow();
            this.Hide();
            this.ShowInTaskbar = false;
        }

        public void RevealWindowForPages()
        {
            this.Show();
            this.ShowInTaskbar = true;

            this.WindowState = windowWasMinimizedPriorToHidingForPage ? WindowState.Minimized 
                : windowWasMaximizedPriorToHidingForPage ? WindowState.Maximized
                : WindowState.Normal;

            if (this.WindowState != WindowState.Minimized)
            {
                this.Activate();
                this.Focus();
            }

            windowWasMinimizedPriorToHidingForPage = false;
            windowWasMaximizedPriorToHidingForPage = false;
        }

        public void SendWindowToPage(int pageIndex, bool skipHide = false)
        {
            if (pageIndex < 1 || pageIndex > 8)
            {
                Message($"Invalid page index: {pageIndex}. Must be from 1 to 8.");
                return;
            }
            
            int currentPageIndex = GetCurrentPageIndex();
            if (currentPageIndex == pageIndex)
            {
                if (!WorkspaceLoadInProgress)
                    Message($"Window is already on page {pageIndex}.");
                return;
            }

            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
            {
                Message($"Window is empty (no image loaded). There's nothing to send.");
                return;
            }

            windowPageIndex = pageIndex;
            if (!skipHide)
                HideWindowForPages();

            // we add one fresh Cloudless window to new pages for UX convenience. But if there is a window sent there, we should remove that convenience window for cleanliness.
            var blankWindowsOnTargetPage = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == pageIndex && string.IsNullOrEmpty(w.currentlyDisplayedImagePath))
                .ToList();

            if (blankWindowsOnTargetPage.Count == 1)
            {
                var windowToRemove = blankWindowsOnTargetPage.First();
                windowToRemove.Close();
            }
        }

        public void SendPageToPage(int pageIndex)
        {
            if (pageIndex < 1 || pageIndex > 8)
            {
                Message($"Invalid page index: {pageIndex}. Must be from 1 to 8.");
                return;
            }

            int currentPageIndex = GetCurrentPageIndex();
            if (currentPageIndex == pageIndex)
            {
                Message($"Window is already on page {pageIndex}.");
                return;
            }

            var windowsToSend = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == currentPageIndex)
                .ToList();

            foreach (var w in windowsToSend)
            {
                w.SendWindowToPage(pageIndex);
            }
        }

        public void SwapViewToPage(int pageIndex, bool skipHide = false)
        {
            if (pageIndex < 1 || pageIndex > 8)
            {
                Message($"Invalid page index: {pageIndex}. Must be from 1 to 8.");
                return;
            }

            int currentPageIndex = GetCurrentPageIndex();
            if (currentPageIndex == pageIndex && !skipHide && !WorkspaceLoadInProgress )
            {
                Message($"Already on page {pageIndex}.");
                return;
            }

            if (!skipHide)
            {
                // minimize all windows on the current page, and also hide them from taskbar and alt-tab
                var windowsToHide = Application.Current.Windows
                    .OfType<MainWindow>()
                    .Where(w => w.windowPageIndex == currentPageIndex)
                    .ToList();

                foreach (var w in windowsToHide)
                {
                    w.HideWindowForPages();
                }
            }
            
            // unminimize all windows on the new page, and show them in taskbar and alt-tab
            var windowsToReveal = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == pageIndex)
                .ToList();

            foreach (var w in windowsToReveal)
            {
                w.RevealWindowForPages();
            }

            // update current page index in settings
            SetCurrentPageIndex(pageIndex);

            if (windowsToReveal.Count == 0)  // improve UX by ensuring a window is present (to receive hotkeys/commands)
            {
                var freshWindow = new MainWindow("");
                freshWindow.Show();
                freshWindow.Activate();
                freshWindow.Focus();
                freshWindow.Message($"Created new Cloudless window since page was empty");
            }
        }

        public void SwapPageWithPage(int p1, int p2)
        {
            if (p1 < 1 || p1 > 8 || p2 < 1 || p2 > 8)
            {
                Message($"Invalid page index: {p1} or {p2}. Must be from 1 to 8.");
                return;
            }
            if (p1 == p2)
            {
                Message($"Cannot swap page {p1} with itself.");
                return;
            }

            var p1Windows = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == p1)
                .ToList();

            var p2Windows = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == p2)
                .ToList();

            foreach (var w in p1Windows)
            {
                w.SendWindowToPage(p2, skipHide: GetCurrentPageIndex() != p1);  // skip hiding if they are already hidden (i.e. not in current page)
            }
            foreach (var w in p2Windows)
            {
                w.SendWindowToPage(p1, skipHide: GetCurrentPageIndex() != p2);
            }

            List<MainWindow> windowsToNowReveal;
            if (GetCurrentPageIndex() == p1)
            {
                windowsToNowReveal = p2Windows;
            }
            else if (GetCurrentPageIndex() == p2)
            {
                windowsToNowReveal = p1Windows;
            }
            else
                return;

            foreach (var w in windowsToNowReveal)  // revealing windows from the page that was just swapped with the current page, if any.
            {
                w.RevealWindowForPages();
            }
        }

        public void ClearPage(int pageIndex)
        {
            if (pageIndex < 1 || pageIndex > 8)
            {
                Message($"Invalid page index: {pageIndex}. Must be from 1 to 8.");
                return;
            }
            var windowsToClose = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w.windowPageIndex == pageIndex)
                .ToList();
            foreach (var w in windowsToClose)
            {
                w.Close();
            }
        }

        public List<int> GetNonemptyPages()
        {
            var pages = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => !string.IsNullOrEmpty(w.currentlyDisplayedImagePath))
                .Select(w => w.windowPageIndex)
                .Distinct()
                .Order()
                .ToList();

            return pages;
        }

        public void FlattenPages(int targetPage = 1)
        {
            var windows = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => !string.IsNullOrEmpty(w.currentlyDisplayedImagePath) && w.windowPageIndex != targetPage)
                .ToList();
            foreach (var w in windows)
            {
                w.SendWindowToPage(targetPage, skipHide: true);
            }
        }
    }

    public class CloudlessWorkspace
    {
        public int SchemaVersion { get; set; } = 4;  // schema version 4 adds per-window loop start/end (backward-compatible)
        public List<CloudlessWindowState> CloudlessWindows { get; set; } = new();
        public string? WorkspaceName { get; set; }
        public int CurrentPageIndex { get; set; } = 1;
    }

    public class CloudlessWindowState
    {
        public string ImagePath { get; set; } = "";

        // Window placement
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // Image view state
        public string DisplayMode { get; set; } = "";  // TODO convert this to an enum, here and elsewhere.
        public double Zoom { get; set; }
        public double PanX { get; set; }
        public double PanY { get; set; }
        public double RenderWidth { get; set; }  // width of image rendering including "beyond what is visible in the window". Useful for cropping.
        public double RenderHeight { get; set; }  // similar to above

        // Optional video loop bounds in milliseconds. Null indicates no custom bound saved in workspace.
        public double? LoopStartMs { get; set; }
        public double? LoopEndMs { get; set; }

        // Optional but useful
        public bool IsMaximized { get; set; }
        public bool IsMinimized { get; set; }
        public int ZOrder { get; set; }  // relative order among Cloudless windows

        public string CloudlessAppVersion { get; set; } = "";

        public int PageIndex { get; set; } = 1;
        public bool WindowWasMinimizedPriorToHidingForPage { get; set; } = false;
        public bool WindowWasMaximizedPriorToHidingForPage { get; set; } = false;
    }

    internal static class NativeMethods
    {
        public const int GW_HWNDNEXT = 2;

        [DllImport("user32.dll")]
        public static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
