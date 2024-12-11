# JustView

TODO

Probably should/will:
-- per-window/instance always-on-top via a hotkey like "T". Also toggles when global setting is always-on-top. In fact, make the setting "start app with always-on-top enabled" then just let T toggle.
-- Add window for hotkey cheatsheet, also list the less obvious features such as drag-and-drop and double-click for fullscreen
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
-- large feature: maybe zooming and panning. ctrl scroll to zoom or ctrl and plus/minus. ctrl 0 and 9 for window fit and true resolution (100%). ctrl click and drag to pan. (click and drag moves window; maybe option to swap which needs CTRL)
- odd jitter of window when changing images, such as when navigating through a directory
- hotkey (only applicable for BestFit) that resizes the window so that the image stays visibly identical, but the black borders disappear.
- implement: holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
- hotkey: zoom to fill window with best fit (only during BestFit)



Probably should but lower priority:
- CTRL ALT or SHIFT C to copy a version of the image that is compressed or downscaled to be under a configured max size, e.g. 4mb to accommodate Discord limits.
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
- If you change a config in one window/instance, how should/can it affec tother windows that are open?


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
CTRL C: copy displayed image to clipboard
C: close window or secondary window
P: open Preferences window
A: open About window
I: open Image Info window if an image is loaded
M: Minimize window
R: Rotate image 90 degrees clockwise
Left and Right: Load adjacent respective images in the directory of the currently loaded image
B: Resize window to remove best-fit bars