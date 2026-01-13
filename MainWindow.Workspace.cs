using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        public string GetVersion()
        {
            return CURRENT_VERION;
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
                DisplayMode = Cloudless.Properties.Settings.Default.DisplayMode,
                Zoom = imageScaleTransform.ScaleX,  // Y would be the same as X regardless
                PanX = imageTranslateTransform.X,
                PanY = imageTranslateTransform.Y,
                ZOrder = zOrder,
                RenderWidth = renderWidth,
                RenderHeight = renderHeight
            };

            return state;
        }

        public static (int, string?) SaveWorkspace(string workspaceName = "MainWorkspace", bool allowOverwrite = false)
        {
            try
            {
                var workspace = new CloudlessWorkspace();
                string workspaceFilePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");

                if (!allowOverwrite && File.Exists(workspaceFilePath)) 
                {
                    return (-2, null);
                }

                var zOrderMap = GetZOrderForCurrentProcessWindows();

                foreach (var window in Application.Current.Windows.OfType<MainWindow>())
                {
                    workspace.CloudlessWindows.Add(window.GetWindowState(GetZOrderForCurrentProcessWindows()));
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(workspace, options);
                File.WriteAllText(workspaceFilePath, json);

                return (workspace.CloudlessWindows.Count, null);
            }
            catch (Exception e)
            {
                // TODO does this need to be static? We could message failure here and return a bool to caller for success. Similar for the load function.
                //Message(e.Message);
                return (-1, e.Message);
            }
        }

        private void CloseAllOtherInstances()
        {
            var windowsToClose = Application.Current.Windows
                .OfType<Window>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToClose)
            {
                w.Close();
            }
        }

        private void MinimizeAllOtherInstances()
        {
            var windowsToMinimize = Application.Current.Windows
                .OfType<Window>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToMinimize)
            {
                w.WindowState = WindowState.Minimized;
            }
        }

        private void UnminimizeAllOtherInstances()
        {
            var windowsToUnminimize = Application.Current.Windows
                .OfType<Window>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in windowsToUnminimize)
            {
                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;
            }
        }

        // returns whether successful
        public bool LoadWorkspace(string workspaceName = "MainWorkspace", bool merge = false)
        {
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

                if (!merge)
                {
                    //Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    this.CloseAllOtherInstances();
                    Thread.Sleep(150); // brief grace period (optional but helps UX)
                    CreateWindowsForWorkspace(workspace); // TODO critical failure point for UX here; what if nothing ever opens?
                    this.Close();
                }
                else
                {
                    CreateWindowsForWorkspace(workspace);
                }
                
                return true;
            }
            catch (FileNotFoundException)
            {
                Message("Workspace file not found.");
                return false;
            }
            catch (Exception)
            {
                Message("Unexpected error loading workspace.");
                return false;
            }
        }

        public static void CreateWindowsForWorkspace(CloudlessWorkspace workspace)
        {
            var zOrderedWindows = workspace.CloudlessWindows.OrderByDescending(w => w.ZOrder).ToList();
            foreach (var state in zOrderedWindows)
            {
                var window = new MainWindow(state.ImagePath, state.Width, state.Height);
                window.ApplyWindowState(state);
                window.Show();
                window.PostProcessLoadedWindow();
            }
        }

        public void ApplyWindowState(CloudlessWindowState state)
        {
            WorkspaceLoadInProgress = true;

            //Cloudless.Properties.Settings.Default.DisplayMode = state.DisplayMode;
            //ApplyDisplayMode();
            // This gets weird and complicated since all instances share the same config file. It might make sense to just default all windows to a best fit mode.
            // Anyway, it's hard to imagine a use case where someone would really want a workstation with mixed modes.

            ResizeWindow(state.Width, state.Height);
            RepositionWindow(state.Left, state.Top);
            if (state.IsMaximized)
                ToggleFullscreen();

            if (state.DisplayMode.ToLower().StartsWith("best"))  // best fit or zoomless best fit
            {
                if (imageScaleTransform == null || imageTranslateTransform == null) throw new NullReferenceException();

                // This essentially applies cropping
                ToggleCropMode(true, true);
                ImageDisplay.Width = state.RenderWidth;
                ImageDisplay.Height = state.RenderHeight;
                
                imageScaleTransform.ScaleX = state.Zoom;
                imageScaleTransform.ScaleY = state.Zoom;
                imageTranslateTransform.X = state.PanX;
                imageTranslateTransform.Y = state.PanY;

                //ToggleCropMode(false, true);  // toggling this here is too early and causes the image to be resized undesirably.
            }

            // TODO maybe clamp windows to monitor bounds or something in case they get sent off screen? Though users may desire that. Anyway users can easily fix a window by focusing it with keyboard and then using something like 'f'.
        }

        public void PostProcessLoadedWindow()
        {
            ToggleCropMode(false, true);
            WorkspaceLoadInProgress = false;
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
    }

    public class CloudlessWorkspace
    {
        public int SchemaVersion { get; set; } = 1;
        public List<CloudlessWindowState> CloudlessWindows { get; set; } = new();
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

        // Optional but useful
        public bool IsMaximized { get; set; }
        public int ZOrder { get; set; }  // relative order among Cloudless windows

        public string CloudlessAppVersion { get; set; } = "";
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
