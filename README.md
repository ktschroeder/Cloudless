# JustView
JustView is a lightweight, minimal-UI image viewer for Windows. It's feature-rich, but visually quiet and minimal.

TODO

Up next:
- bug: going from display to exploration mode often makes parts of image cut-off (visible when panning), regardless of display type. Seems related to window size.
- ability to quickly load recent images (esp. useful for development/debugging). ==> history.

Probably should/will:
- messages and blocks for things that cannot be done with GIFs. Messaging about longer load times for GIFs (try opening local vs dragging from web)
- similar to with panning, prevent zooming out beyond image (i.e. when both X and Y axes would have blackspace at same time)
- setting to mute messages
- ability to copy image itself rather than just image file (can we "do both" dynamically?)
- provide a simple window that can be accessed, showing all messages from this session.
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- odd jitter of window when changing images, such as when navigating through a directory
- implement: holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
- allow focus/interaction on main window while secondary windows are open? In particualr may be useful for hotkey reference.

Probably should but lower priority:
- For zoom-to-fill ==> exploration mode, could probably make seamless by simulating the zooming in expl mode to match the previous view.
- bug: right click to context menu then left click on main window is treated as double-click
-- Add window for hotkey cheatsheet, also list the less obvious features such as drag-and-drop and double-click for fullscreen
- hotkey: zoom to fill window with best fit (only during BestFit)
- extended context window (Secondary?) so that nothing "must" be done with a hotkey. maybe.
- other filetypes that MAY be simple to add/handle: TIFF, HEIF, HEIC, SVG, ICO
- optionally, recent files, where disabling it also clears the history (or just have button to clear history). Windows natively provides a history function when right clicking on icon on taskbar. Manage and given option to enable/disable this?
- loading GIF takes a while, may be good to show "Loading..." text or something. User cursor stays weird? Possibly blocks stuff?
- styling
- right click option to set window to typed dimensions
- customizable hotkeys
- graceful error handling
- Clean up code, especially when "done" with everything else
- option: snap to best fit when dragging border
- allow thru-window transparency? option to show transparency as black, or something else (gray checker? see what other apps do), or thru-window. ==> possibility of appearance of entirely floating image, would be neat.
- consider whether/what features should be behind feature flags users can configure in settings (to keep app/UI cleaner if they like). Also consider some kind of power user mode for this.
- If you change a config in one window/instance, how should/can it affect other windows that are open?
- optional online listening for updates.


Maybe / ideas:
- WEBM support? Maybe a very basic kind. Certainly not a video player.
- should window do something else other than move to center of screen when changing image etc.?
- possibly other z-index shenanigans beyond always-on-top
- intuitive behavior of multiple windows
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- maybe logo for app, other name
- maybe option for dropshadow beyond window
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.



Hotkeys:

F11: toggle fullscreen
Esc: exit fullscreen, or close a secondary window such as Preferences
F: set window size to image's true dimensions if possible, not exceeding the screen size. If the setting to leave a buffer is enabled, then pressing F subsequently will toggle between the buffer size and the bufferless size.
O: open an image
V: maximize vertical dimension and align window
CTRL C: copy displayed image file to clipboard
CTRL ALT C: convert displayed image to JPG, and compress iteratively until it is below a configured max file size, then copy the resulting image file to clipboard
C: close window or secondary window
P: open Preferences window
A: open About window
I: open Image Info window if an image is loaded
M: Minimize window
R: Rotate image 90 degrees clockwise
Left and Right: Load adjacent respective images in the directory of the currently loaded image
B: Resize window to remove best-fit bars
T: Toggle always-on-top for the focused window