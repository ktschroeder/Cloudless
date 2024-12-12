# JustView
JustView is a lightweight, minimal-UI image viewer for Windows. It's feature-rich, but visually quiet and minimal.

TODO

Up next:
*-* bug: going from display to exploration mode often makes parts of image cut-off (visible when panning), regardless of display type. Seems related to window size. Other times, can pan more than you should be able to (blackspace around borders)? Maybe a bad order of panning/zooming somewhere in the transition.
- (fix bug first as it could impact this) similar to with panning, prevent zooming out beyond image (i.e. when both X and Y axes would have blackspace at same time)
	- Conceptually simple approach may be: if new position would be out of bounds, then nudge back to in-bounds. 

Probably should/will:
*-* messages and blocks for things that cannot be done with GIFs. Messaging about longer load times for GIFs (try opening local vs dragging from web)
*-* odd jitter of window when changing images, such as when navigating through a directory
*-* ability to copy image itself rather than just image file (can we "do both" dynamically? If not, ctrl shift c would be okay.) 
- provide a simple window that can be accessed, showing all messages from this session.
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- implement: holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
- bug: fullscreen on secondary monitor, click and drag, image uses cursor location as if on primary monitor

Probably should but lower priority:
- Good name may be Cloudless View. Can have start-up screen be simple sky-ish drawing or a solid gentle blue.
- For zoom-to-fill ==> exploration mode, could probably make seamless by simulating the zooming in expl mode to match the previous view.
- bug: right click to context menu then left click on main window is treated as double-click
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
- somewhere list the less obvious features such as drag-and-drop and double-click for fullscreen
