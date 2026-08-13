namespace Cloudless.ReferenceData
{
    public class ReferenceItem
    {
        public string Key { get; set; }
        public string Description { get; set; }

        public ReferenceItem(string key, string description)
        {
            Key = key;
            Description = description;
        }
    }

    public class ReferenceTab
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public List<ReferenceItem> Items { get; set; }
        public string Footer { get; set; }

        public ReferenceTab(string header, List<ReferenceItem> items, string description = null, string footer = null)
        {
            Header = header;
            Items = items;
            Description = description;
            Footer = footer;
        }
    }

    public static class HotkeyReferenceData
    {
        public static List<ReferenceTab> GetTabs()
        {
            return new List<ReferenceTab>
            {
                new ReferenceTab("General", new List<ReferenceItem>
                {
                    new ReferenceItem("X", "Show the context menu (as if you've right-clicked)"),
                    new ReferenceItem("C", "Close window or secondary window"),
                    new ReferenceItem("M", "Minimize window"),
                    new ReferenceItem("Esc", "Exit fullscreen or close a secondary window"),
                    new ReferenceItem("F11 or double-click", "Toggle fullscreen"),
                    new ReferenceItem("Z", "Toggle Zen"),
                    new ReferenceItem("Ctrl Z", "Start Zen on all open windows, or stop Zen on all open windows if already on everywhere"),
                    new ReferenceItem("Ctrl C", "Copy image file to clipboard"),
                    new ReferenceItem("Ctrl Alt C", "Copy compressed image file to clipboard (configure size max in preferences)"),
                    new ReferenceItem("Ctrl W", "Set current image as desktop wallpaper"),
                    new ReferenceItem("Ctrl Alt W", "Apply the window's view to desktop wallpaper (includes pan/zoom/crop)"),
                    new ReferenceItem("B", "Bookmark the current image. Or, with Shift held, unbookmark the current image."),
                }),
                new ReferenceTab("View", new List<ReferenceItem>
                {
                    new ReferenceItem("F", "Set window size to image's true dimensions, or as big as possible without going off-screen"),
                    new ReferenceItem("V", "Maximize vertical dimension"),
                    new ReferenceItem("Q", "Toggle crop mode"),
                    new ReferenceItem("Ctrl Q", "Enter crop selection mode: Click and drag to select an area to crop to (Esc to cancel)"),
                    new ReferenceItem("G", "Switch background (black, white, transparent)"),
                    new ReferenceItem("R", "Rotate image 90° clockwise"),
                    new ReferenceItem("B", "Resize window to remove best-fit bars"),
                    new ReferenceItem("Ctrl + / -", "Zoom in / out"),
                    new ReferenceItem("Ctrl 0", "Reset zoom/pan"),
                    new ReferenceItem("Ctrl 9", "Set zoom to image's true resolution"),
                    new ReferenceItem("Ctrl, scroll mouse", "Zoom in or out. Additionally, hold ALT for fine zooming"),
                    new ReferenceItem("Ctrl, click and drag", "Pan image"),
                    new ReferenceItem("L", "Zoom image to fill view"),
                    new ReferenceItem("Ctrl E", "Toggle 'Comic Mode': when loading next image in directory, retain zoom, and send pan to upper corner"),
                }),
                new ReferenceTab("Image", new List<ReferenceItem>
                {
                    new ReferenceItem("O", "Open an image"),
                    new ReferenceItem("Left/Right or A/D", "Load adjacent images in directory of current image, based on current sort order"),
                    new ReferenceItem("Mousewheel up/down", "Similar to above"),
                    new ReferenceItem("Ctrl F", "Open or close Film Strip"),
                }),
                new ReferenceTab("Video", new List<ReferenceItem>
                {
                    new ReferenceItem("U", "Toggle video controls UI"),
                    new ReferenceItem("Ctrl M", "Toggle mute/unmute for that window"),
                    new ReferenceItem("Space", "Pause/resume GIF/video"),
                    new ReferenceItem("Ctrl Space", "Restart GIF/video"),
                    new ReferenceItem("Ctrl Left/Right", "Seek backward/forward 5 seconds"),
                    new ReferenceItem("Ctrl Alt Left/Right", "Seek backward/forward 60 seconds"),
                    new ReferenceItem("Ctrl Shift Left/Right", "Seek backward/forward finely"),
                }),
                new ReferenceTab("Windows", new List<ReferenceItem>
                {
                    new ReferenceItem("P", "Open Preferences window"),
                    new ReferenceItem("Ctrl A", "Open About window"),
                    new ReferenceItem("H", "Open Hotkey Reference window"),
                    new ReferenceItem("Ctrl H", "Open Command Palette Reference window"),
                    new ReferenceItem("I", "Open Image Info window (if one is loaded)"),
                    new ReferenceItem("Ctrl R", "Open Recent Images Gallery"),
                    new ReferenceItem("Ctrl Shift M", "Open System Messages window"),
                    new ReferenceItem("Ctrl D", "Duplicate window"),
                    new ReferenceItem("Ctrl N", "New window"),
                    new ReferenceItem("E", "Reveal current image/video in Windows File Explorer"),
                }),
                new ReferenceTab("Advanced", new List<ReferenceItem>
                {
                    new ReferenceItem("T", "Toggle always-on-top"),
                    new ReferenceItem("Ctrl T", "Toggle always-on-bottom"),
                    new ReferenceItem("Ctrl Alt D", "Toggle debug info panel"),
                    new ReferenceItem("Shift (see note)", "Hold when moving window to constrain movement to straight lines (must hold at start)"),
                    new ReferenceItem("; or :", "Open Command Palette (for more info, see Command Palette Reference)"),
                    new ReferenceItem("Long-hold right click", "Swap mouse mode to Mouse Control Mode (normal mouse behavior replaced by the 2 below)"),
                    new ReferenceItem("(MCM) Click and drag", "Pan image"),
                    new ReferenceItem("(MCM) Scroll up/down", "Zoom in/out"),
                    new ReferenceItem("Middle click", "Open the quick command display, showing clickable buttons for custom user commands"),
                    new ReferenceItem("Long-hold middle click", "Swap mouse mode to an unused, third mouse mode (this currently has no effect on behavior)"),
                    new ReferenceItem("Double Middle click", "Toggle crop mode (same as hotkey Q)"),
                    new ReferenceItem("Number keys 1-8", "Change view to the corresponding page. See command palette reference for more info about pages."),
                }),
            };
        }
    }
}
