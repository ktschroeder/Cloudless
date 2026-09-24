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
                    new ReferenceItem("Ctrl [1 through 8]", "Run custom commands stored at the respective index (commands 1-8)"),
                    new ReferenceItem("Ctrl Alt [1 through 8]", "Run custom commands stored at the respective index (commands 9-16)"),
                    new ReferenceItem("Ctrl Alt Shift [1 through 8]", "Run custom commands stored at the respective index (commands 17-24)"),
                    new ReferenceItem("(palette) Tab", "For workspace commands (e.g. 'ws l [name]'), autocomplete workspace name via substring search, or cycle through matches. For tag queries (e.g. 'fs tag'), autocomplete tag name."),
                    new ReferenceItem("(palette) Shift Tab", "Similar to above, but cycle in the reverse direction."),
                    new ReferenceItem("(palette) Ctrl Tab", "For workspace commands (e.g. 'ws l [name]'), traverse workspace names sorted by recency of save/load. For tag queries, cycle through tags."),
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
                    new ReferenceItem("c[Int 1-24] set [String]", "Save custom command [String] at index [Int]"),
                    new ReferenceItem("c[Int 1-24] view", "View command stored at index [Int]"),
                    new ReferenceItem("c[Int 1-24] run", "Run command stored at index [Int]"),
                    new ReferenceItem("ris", "Perform an online reverse-image-search for the current image, including uploading it to a temporary host."),
                    new ReferenceItem("ris [service]", "See above. [service] can be google, bing, yandex, tineye, or saucenao. Their first letters can also be used."),
                    new ReferenceItem("shutdown | sd", "Close all open instances of Cloudless, and shutdown the Cloudless background process"),
                    new ReferenceItem("deflash", "Minimize and unminimize all windows (useful if Windows piles up flashing taskbar icons)"),
                    new ReferenceItem("nudge | n left/right/up/down [int]", "Move the window in the specified direction, a distance of [int] pixels. First letters for directions can also be used."),
                    new ReferenceItem("nudge | n [int x] [int y]", "Move the window by a vector [int x],[int y]"),
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
                new ReferenceTab("Tags", new List<ReferenceItem>
                {
                    new ReferenceItem("tag | t add | a [tag1] [tag2] ...", "Add one or more space-separated tags to the currently displayed image"),
                    new ReferenceItem("tag | t remove | r [tag1] [tag2] ...", "Remove one or more space-separated tags from the currently displayed image"),
                    new ReferenceItem("tag | t destroy [tag]", "Delete a tag entirely (removes it from all media regardless of what is loaded)"),
                    new ReferenceItem("tag | t list | l", "List all tags associated with the current window's media"),
                    new ReferenceItem("filmstrip | fs tag | t [query]", "Open filmstrip and populate it with images matching the tag query. Supports Boolean operators like 'tag1 AND tag2', 'tag1 OR tag2', 'NOT tag1', etc. Parentheses for logical grouping. Use tab autocomplete to search/cycle through tag names."),
                    new ReferenceItem("open | o tag | t [query]", "Open images matching the tag query (max 10). Supports same Boolean operators as above. Use tab autocomplete for tag search."),
                    new ReferenceItem("open! | o! tag | t [query]", "Open all images matching the tag query (ignore max). See above for more notes."),
                    new ReferenceItem("gallery tag | t [query]", "Open gallery view of images matching the tag query. Supports same Boolean operators as above."),
                }, "Tags are logic-ready, searchable labels you can apply to individual media. Use tab autocomplete when entering tag queries to search and cycle through available tags."),
                new ReferenceTab("Image", new List<ReferenceItem>
                {
                    new ReferenceItem("[Int]", "Jump to the image with index [Int] in the current directory"),
                    new ReferenceItem("+[Int]", "Jump [Int] images forward in directory"),
                    new ReferenceItem("-[Int]", "Jump [Int] images backward in directory"),
                    new ReferenceItem("r", "Open most recently loaded image"),
                    new ReferenceItem("r [Int]", "Open the [Int] most recent images, in new windows"),
                    new ReferenceItem("first | last", "Open first or last image in current directory"),
                    new ReferenceItem("/[query]", "Open the next image in current directory whose filename contains [query], case insensitive. Wraps around at end."),
                    new ReferenceItem("sort [type] [order]", "Set directory sort order. Type can be 'name' or 'date'. Order can be 'asc' or 'desc'."),
                    new ReferenceItem("o [path]", "Open the image at [path], which may be relative or absolute. Or open all images in directory (max 10)"),
                    new ReferenceItem("o! [path]", "Open the image at [path], which may be relative or absolute. Or open all images in directory (ignore max)"),
                }),
                new ReferenceTab("Video", new List<ReferenceItem>
                {
                    new ReferenceItem("mute", "Mute the window's audio"),
                    new ReferenceItem("unmute", "Unmute the window's audio"),
                    new ReferenceItem("volume", "Show a message with the window's current volume level (0-100)"),
                    new ReferenceItem("volume [number]", "Set the window's voilume to [number] (0-100)"),
                    new ReferenceItem("set start | s", "Set a custom start point for looping, based on the current position. Resets upon loading anything else."),
                    new ReferenceItem("set end | e", "Set a custom end point for looping, based on the current position. Resets upon loading anything else."),
                    new ReferenceItem("clear start/end | s/e", "Clear any custom start/end points for looping"),
                    new ReferenceItem("seek ?", "Reveal current seeking time of video"),
                    new ReferenceItem("seek [absolute time]", "Seek to the specified time in the video. Formats: '90' (seconds), '1:30' (minutes:seconds), '1:30:45' (hours:minutes:seconds), or '1h30m45s'"),
                    new ReferenceItem("seek -[relative time]", "Seek backward the duration given"),
                    new ReferenceItem("seek +[relative time]", "Seek forward the duration given"),
                    new ReferenceItem("goto start/end", "Seek to the video start/end (or custom start/end, if set)"),
                    new ReferenceItem("play", "Play/resume the current video"),
                    new ReferenceItem("pause", "Pause the current video"),
                    new ReferenceItem("set/clear flag | f", "Set/Clear a custom flag at the current time in this video. (Useful for cross-instance coordination)"),
                    new ReferenceItem("goto flag | f", "Seek to the custom flag time, if one is set"),
                    new ReferenceItem("sync", "Register this video in the page's sync group. All synced videos on a page wait to restart until all synced videos on the page finish"),
                    new ReferenceItem("unsync", "Unregister this video from the page's sync group"),
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
                    new ReferenceItem("ws list [media path]", "List all saved workspaces containing the specified media path"),
                }, "A \"workspace\" is the collective arrangement of all open Cloudless windows. When you save a workspace, you can later load it to quickly open all its media, with their identical position, zoom, pan, crop, and Z-order. Also, any custom video start/end points are saved/applied.", "With \"ws l\", \"ws s\", etc., press tab to search name (press repeatedly to traverse matches. Hold Shift to reverse direction)"),
                new ReferenceTab("Pages", new List<ReferenceItem>
                {
                    new ReferenceItem("p [target]", "Change view to page [target]"),
                    new ReferenceItem("p ?", "Display a message showing the currently open page index, and all indices of non-empty pages"),
                    new ReferenceItem("p [target] send | s", "Send the current window to page [target]"),
                    new ReferenceItem("p [target] bring | b", "Send the current window to page [target], and change view to that page"),
                    new ReferenceItem("p [target] send | s page | p", "Send all windows in the current page to page [target]"),
                    new ReferenceItem("p [target] bring | b page | p", "Send all windows in the current page to page [target], and change view to that page"),
                    new ReferenceItem("p [target] clear", "Close all windows on page [target]"),
                    new ReferenceItem("p [target] swap p [target]", "Swap all windows between page [target] and page [target] (the order of the 2 targets does not matter)"),
                    new ReferenceItem("flatten", "Send all windows from all pages to page 1, and change view to page 1"),
                    new ReferenceItem("slideshow | ss [seconds]", "Start automatic slideshow: cycle through active pages every [seconds] seconds"),
                    new ReferenceItem("slideshow | ss [seconds] shuffle", "Similar to above, but traverse pages in smart-random order"),
                    new ReferenceItem("slideshow | ss [optionally 'shuffle'] triggers", "Start slideshow, where pre-configured videos can serve as page-turn triggers. Optional 'shuffle' is explained above"),
                    new ReferenceItem("slideshow | ss stop", "Stop the current slideshow"),
                    new ReferenceItem("slideshow | ss next", "Advance to the next slide in the current slideshow without stopping the slideshow"),
                    new ReferenceItem("set/clear trigger", "Set or clear a trigger for current window and video: Triggers fire when a video ends and can be used as a page-turn trigger in slideshows"),
                    new ReferenceItem("set trigger [int]", "Set a trigger (see above), but the video must finish [int] times before the trigger fires"),
                }, "A \"page\" is similar to a simple workspace, but all pages' windows are kept in memory simultaneously. A workspace can contain up to 20 pages. The benefit of this is that you can swap full arrangements (pages) almost immediately, whereas loading a different workspace could take longer. It also enables more complex and organized workspaces with manageable layers, and enables slideshows. Beware of performance losses if you have lots of media in active memory. For commands with a [target], the target can be a page index, or any of: t (this), p (prev), n (next), pa/na (prev/next active), or pi/ni (prev/next inactive). Pages are considered active if they have any windows with loaded media."),
                new ReferenceTab("Advanced", new List<ReferenceItem>
                {
                    new ReferenceItem("hotkey | hk [modifiers] [key]", "Simulate a hotkey press, e.g. 'hotkey ctrl shift c'. Useful if you want a command for a hotkey-only feature."),
                    new ReferenceItem("[command 1]; [command 2]; ...", "Chain multiple commands together using semicolons. Each command will be executed in sequence."),
                    new ReferenceItem("all [command]", "Execute [command] on each other window, and finally this window. (Use with caution.)"),
                    new ReferenceItem("others [command]", "Execute [command] on each other window. (Use with caution.)"),
                    new ReferenceItem("macro record [name] [command]", "Record custom command [command] that is saved and can be invoked with name [name]"),
                    new ReferenceItem("macro run [name]", "Run the custom command saved with name [name]"),
                    new ReferenceItem("macro delete [name]", "Delete the custom command saved with name [name]"),
                    new ReferenceItem("macro list", "List all saved custom commands"),
                }),
            };
        }
    }
}
