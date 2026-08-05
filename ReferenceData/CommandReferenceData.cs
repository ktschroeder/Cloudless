namespace Cloudless.ReferenceData
{
    public static class CommandReferenceData
    {
        public static List<ReferenceTab> GetTabs()
        {
            return new List<ReferenceTab>
            {
                new ReferenceTab("Non-Command", new List<ReferenceItem>
                {
                    new ReferenceItem("':' or ';'", "Open command palette"),
                    new ReferenceItem("(palette) Enter", "Submit command"),
                    new ReferenceItem("(palette) Esc", "Cancel/exit the command palette"),
                    new ReferenceItem("(palette) Up and Down", "Traverse through history of commands (globally)"),
                    new ReferenceItem("Ctrl ';'", "Execute the most recent valid command"),
                    new ReferenceItem("Ctrl [1 through 8]", "Run custom commands stored at the respective index"),
                    new ReferenceItem("(palette) Tab", "For workspace commands (e.g. 'ws l [name]'), autocomplete workspace name, or cycle through matches."),
                    new ReferenceItem("(palette) Shift Tab", "Similar to above, but cycle in the reverse direction."),
                    new ReferenceItem("(palette) Ctrl Tab", "For workspace commands (e.g. 'ws l [name]'), traverse workspace names sorted by recency of save/load."),
                    new ReferenceItem("(palette) Ctrl Shift Tab", "Similar to above, but cycle in the reverse direction."),
                }, "The command palette provides advanced features using a text-based interface."),
                new ReferenceTab("General", new List<ReferenceItem>
                {
                    new ReferenceItem("cip", "Copy image path to clipboard"),
                    new ReferenceItem("rev", "Reveal current image in File Explorer"),
                    new ReferenceItem("c", "Close window"),
                    new ReferenceItem("c all / others", "Close all windows, or all other windows"),
                    new ReferenceItem("help", "Open command palette reference window"),
                    new ReferenceItem("m all / others", "Minimize all windows, or all other windows"),
                    new ReferenceItem("um all", "Unminimize all windows"),
                    new ReferenceItem("c[Int 1-8] set [String]", "Save custom command [String] at index [Int]"),
                    new ReferenceItem("c[Int 1-8] view", "View command stored at index [Int]"),
                    new ReferenceItem("c[Int 1-8] run", "Run command stored at index [Int]"),
                    new ReferenceItem("ris", "Perform an online reverse-image-search for the current image, including uploading it to a temporary host."),
                    new ReferenceItem("ris [service]", "See above. [service] can be google, bing, yandex, tineye, or saucenao. Their first letters can also be used."),
                    new ReferenceItem("shutdown | sd", "Close all open instances of Cloudless, and shutdown the Cloudless background process"),
                    new ReferenceItem("hotkey [modifiers] [key]", "Simulate a hotkey press, e.g. 'hotkey ctrl shift c'. Useful if you want a command for a hotkey-only feature."),
                    new ReferenceItem("[command 1]; [command 2]; ...", "Chain multiple commands together using semicolons. Each command will be executed in sequence."),
                    new ReferenceItem("deflash", "Minimize and unminimize all windows (useful if Windows piles up flashing taskbar icons)"),
                    new ReferenceItem("all [command]", "Execute [command] on each other window, and finally this window. (Use with caution.)"),
                    new ReferenceItem("others [command]", "Execute [command] on each other window. (Use with caution.)"),
                }),
                new ReferenceTab("View", new List<ReferenceItem>
                {
                    new ReferenceItem("dm [mode]", "Set display mode: stretch/zoom/best/bestnozoom. Numbers 1-4 can also be used."),
                    new ReferenceItem("dim [Int] [Int]", "Set window dimensions to [Int]x[Int]"),
                    new ReferenceItem("filmstrip | fs directory | d", "Open filmstrip and populate it with images in the current image's directory"),
                    new ReferenceItem("filmstrip | fs recent | r", "Open filmstrip and populate it with your recent images"),
                    new ReferenceItem("filmstrip | fs bookmark | b", "Open filmstrip and populate it with your bookmarked images"),
                    new ReferenceItem("filmstrip | fs preview | p [workspace]", "Open filmstrip and populate it with images from [workspace]"),
                }),
                new ReferenceTab("Image", new List<ReferenceItem>
                {
                    new ReferenceItem("[Int]", "Jump to the image with index [Int] in the current directory"),
                    new ReferenceItem("+[Int]", "Jump [Int] images forward in directory"),
                    new ReferenceItem("-[Int]", "Jump [int] images backward in directory"),
                    new ReferenceItem("p", "Open most recently loaded image"),
                    new ReferenceItem("first | last", "Open first or last image in current directory"),
                    new ReferenceItem("/[query]", "Open the next image in current directory whose filename contains [query], case insensitive. Wraps around at end."),
                    new ReferenceItem("sort [type] [order]", "Set directory sort order. Type can be 'name' or 'date'. Order can be 'asc' or 'desc'."),
                    new ReferenceItem("o [path]", "Open the image at [path], which may be relative or absolute. Or open all images in directory (max 10)"),
                    new ReferenceItem("o! [path]", "Open the image at [path], which may be relative or absolute. Or open all images in directory (ignore max)"),
                    new ReferenceItem("rec [Int]", "Open the [Int] most recent images, in new windows"),
                }),
                new ReferenceTab("Video", new List<ReferenceItem>
                {
                    new ReferenceItem("set start", "Set a custom start point for looping, based on the current position. Resets upon loading anything else."),
                    new ReferenceItem("set end", "Set a custom end point for looping, based on the current position. Resets upon loading anything else."),
                    new ReferenceItem("clear start/end", "Clear any custom start/end points for looping"),
                    new ReferenceItem("time", "Reveal current seeking time of video"),
                    new ReferenceItem("time [time]", "Seek to the specified time in the video. Formats: '90' (seconds), '1:30' (minutes:seconds), '1:30:45' (hours:minutes:seconds), or '1h30m45s'"),
                }),
                new ReferenceTab("Workspace", new List<ReferenceItem>
                {
                    new ReferenceItem("ws save | s [name]", "Save current workspace to storage as [name]"),
                    new ReferenceItem("ws save! | s! [name]", "Save/overwrite current workspace to storage as [name]"),
                    new ReferenceItem("ws load | l [name]", "Load workspace [name] and close all currently open Cloudless windows"),
                    new ReferenceItem("ws merge | m [name]", "Merge workspace [name] (load it without closing any windows)"),
                    new ReferenceItem("ws rev", "In File Explorer, reveal directory of workspace (.cloudless) files"),
                    new ReferenceItem("qs", "Quicksave: Save current workspace to an unnamed workspace, which can be quickloaded. This always overwrites."),
                    new ReferenceItem("qs c", "Quicksave (see above), and then close all windows if successful"),
                    new ReferenceItem("ql", "Quickload: load the quicksave workspace"),
                    new ReferenceItem("qm", "Quickmerge: merge the quicksave workspace (load it without closing any windows)"),
                    new ReferenceItem("ws preview | p [name]", "Open gallery view of workspace preview. Or, use 'ws p' to open blank preview gallery window."),
                    new ReferenceItem("ws rename | r [old] [new]", "Rename workspace [old] to [new]"),
                    new ReferenceItem("ws delete [name]", "Delete workspace [name]"),
                    new ReferenceItem("ws origin", "Reveal the workspace's name from which this window's current image originated, if any"),
                    new ReferenceItem("ws origin s | s!", "Overwrite current window's origin workspace with the current global workspace state (no overwrite warning)"),
                    new ReferenceItem("ws origin load", "Load the workspace associated with the current image."),
                    new ReferenceItem("c | m | um origin", "Close, minimize, or unminimize all windows that share a workspace origin with the current window"),
                    new ReferenceItem("ws undoload", "Load the open workspace version that was present prior to the most recent 'ws load' or 'ws undoload' command."),
                }, "A \"workspace\" is the collective arrangement of all open Cloudless windows. When you save a workspace, you can later load it to quickly open all its media, with their identical position, zoom, pan, crop, and Z-order. Also, any custom video start/end points are saved/applied.", "With \"ws l\", \"ws s\", etc., press tab to autocomplete name (press repeatedly to traverse matches. Hold Shift to reverse direction)"),
                new ReferenceTab("Pages", new List<ReferenceItem>
                {
                    new ReferenceItem("p[int]", "Change view to page [int]"),
                    new ReferenceItem("p?", "Display a message showing the currently open page index, and all indices of non-empty pages"),
                    new ReferenceItem("p[int] send", "Send the current window to page [int]"),
                    new ReferenceItem("p[int] bring", "Send the current window to page [int], and change view to that page"),
                    new ReferenceItem("p[int] send page", "Send all windows in the current page to page [int]"),
                    new ReferenceItem("p[int] bring page", "Send all windows in the current page to page [int], and change view to that page"),
                    new ReferenceItem("p[int] clear", "Close all windows on page [int]"),
                    new ReferenceItem("p[int] swap p[int]", "Swap all windows between page [int] and page [int] (the order of the 2 ints does not matter)"),
                    new ReferenceItem("flatten", "Send all windows from all pages to page 1, and change view to page 1"),
                    new ReferenceItem("pp / pn", "Change the view to the previous/next page (wraps around at end)"),
                    new ReferenceItem("ppa / pna", "Change the view to the previous/next active page (wraps around at end)"),
                    new ReferenceItem("ss [seconds] | slideshow [seconds]", "Start automatic slideshow: cycle through active pages every [seconds] seconds"),
                    new ReferenceItem("ss stop | slideshow stop", "Stop the current slideshow"),
                }, "A \"page\" is similar to a simple workspace, but all pages' windows are kept in memory simultaneously. A workspace can contain up to 8 pages. The benefit of this is that you can swap full arrangements (pages) almost immediately, whereas loading a different workspace could take longer. It also enables more complex and organized workspaces with manageable layers."),
            };
        }
    }
}
