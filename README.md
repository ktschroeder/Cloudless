# Cloudless
Cloudless is a lightweight, minimal-UI image viewer for Windows. It's feature-rich, but visually quiet and minimal.

TODO

Up next:

Probably should/will:
- compare performance of app before and after adding Zen to identify any performance issues
- messages and blocks for things that cannot be done with GIFs. Messaging about longer load times for GIFs (try opening local vs dragging from web)
- ability to copy image itself rather than just image file (can we "do both" dynamically? If not, ctrl shift c would be okay.)
- possible bug? There may be like 10 pixels fo extra width on the window. Most obvious if fullscreen on one monitor when using multiple.

Probably should but lower priority:
- indicator of current zoom amount upon zooming
- mouse-friendly controls: scroll when not holding CTRL to navigate directory, maybe.
- button to reveal in file explorer for current image. could be on context menu or button in image info window.
- revisit whether/how image should automatically pan/zoom when resizing window in expl mode.
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- implement: holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
- ClampCurrentTransformToIntuitiveBounds sometimes apparently leaves a thin black line
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
- Clean up code, especially when "done" with everything else. Organize methods. Address TODOs commented in code.
- option: snap to best fit when dragging border
- allow thru-window transparency? option to show transparency as black, or something else (gray checker? see what other apps do), or thru-window. ==> possibility of appearance of entirely floating image, would be neat.
- consider whether/what features should be behind feature flags users can configure in settings (to keep app/UI cleaner if they like). Also consider some kind of power user mode for this.
- If you change a config in one window/instance, how should/can it affect other windows that are open?
- optional online listening for updates.
- odd jitter of window when changing images, such as when navigating through a directory. Possibly a WPF issue; a few attempts did not resolve this.

Maybe / ideas:
- configurable gestures: hold left and right click, then drag in a direction. 4 options. and/or middle click.
- special message or label for filename that either persists or fades after a few seconds. Configurable.
- WEBM support? Maybe a very basic kind. Certainly not a video player.
- should window do something else other than move to center of screen when changing image etc.?
- possibly other z-index shenanigans beyond always-on-top
- intuitive behavior of multiple windows
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- maybe logo for app, other name
- maybe option for dropshadow beyond window
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.
- somewhere list the less obvious features such as drag-and-drop and double-click for fullscreen

Features noted in Windows Photos app:
- True number displayed for "zoom zmount" where 100% is true resolution, even when best-fit in small window
- Slider for zoom, and dropdown for common round values like 50%, 500%.
- "film strip" feature: thumbnails (in directory of current image?) for quickly selecting another image to view
- favorites feature (?)
- "delete" feature (with setting for whether to show confirmation dialog first)
- print feature
- set as background/wallpaper etc.
- filename displayed on title
- additional window with "gallery" "Favorites" (includes search of these)
- theme: light or dark or system default
- resizing window behavior when not simply best-fit?