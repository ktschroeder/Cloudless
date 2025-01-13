# Cloudless
Cloudless is a lightweight, minimal-UI image viewer for Windows. It's feature-rich, but visually quiet and minimal.

TODO

Up next:


Do before release:
- messages window: make it possible to copy a message. Add label when there are no messages explaining what the window is (or just always)
- loading GIF takes a while, may be good to show "Loading..." text or something. User cursor stays weird? Possibly blocks stuff? (try opening local vs dragging from web)
- styling
- more zen below
- code and UI clean-up
- create actual README and improve contents of secondary windows. List features and hotkeys in README. e.g. ability to drag onto window.
- create new public repo, Cloudless.
- check TODOs in code
- look into optional online listening for updates.
- look again into drop shadow (see notes elsewhere)
- logo? e.g. for taskbar. Cloud outline or aurora borealis shapes or starry sky maybe.
- True number displayed for "zoom amount" where 100% is true resolution, even when best-fit in small window. Show briefly and fade after changing zoom.
- Options in context menu (Secondary?) for common round values like 50%, 500%.
- thorough testing


Lower priority:
- Window resizing is weirdly slow when holding a modifier key
- odd jitter of window when changing images, such as when navigating through a directory. Possibly a WPF issue; a few attempts did not resolve this. Possibly related, general jitteriness when resizing and similar. Would be good to smoothen out. ==> second pass: does indeed seem to be a WPF/Windows issue that may not have a feasible solution. Concept idea to explore next: "slot window" to swap with current once ready?
- implement ratio-preserving resizing when click-dragging corner, described more below. Pinning at side, and at center, are not very applicable to image viewing on desktop, so never mind those.
	- holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
	- when also with alt or shift, pin at center of window. 
	- looking at photoshop: holding shift does this, pinning the opposite corner. The resizing occurs as if there is a tangent line to the corner you started click/dragging, that is a 45 deg angle from either line of the rectangle, and this line keeps its angle and follows the cursor.
		- when you also hold alt, this resizing happens pinned from the center of the rectangle.
		- when dragging from a side instead of corner, object is pinned at the center of the opposite edge.
- Windows natively provides a history function when right clicking on icon on taskbar. Manage and give option to enable/disable this? Align with in-app list?
- consider thumbnails for recent image list if feasible and okay UI/UX
- bug: when fullscreen, right click to context menu then left click on main window is treated as double-click
- Zen: look back at earlier approach (in feature branch), offer both as different styles.
- Zen: option for "darker zen": just make the BG black. due to opacities, overall effect is more mellow, and also occasionally shows space-esque black or near black which can be neat. But I think light by default is good: seems more visually pleasant.
- Zen: compare performance of app before and after adding Zen to identify any performance issues. Probably make it off or much easier by default.
- mouse-friendly controls: scroll when not holding CTRL to navigate directory, maybe.
- revisit whether/how image should automatically pan/zoom when resizing window in expl mode.
- For zoom-to-fill ==> exploration mode, could probably make seamless by simulating the zooming in expl mode to match the previous view.
- hotkey: zoom to fill window with best fit (only during BestFit)
- extended context window (Secondary?) so that nothing "must" be done with a hotkey. maybe.
- other filetypes that MAY be simple to add/handle: TIFF, HEIF, HEIC, SVG, ICO
- styling
- customizable hotkeys
- graceful error handling
- Clean up code, especially when "done" with everything else. Organize methods. Address TODOs commented in code.
- option: snap to best fit when dragging border (related to other feature: resizing while maintaining aspect ratio of window)
- consider whether/what features should be behind feature flags users can configure in settings (to keep app/UI cleaner if they like). Also consider some kind of power user mode for this.
- If you change a config in one window/instance, how should/can it affect other windows that are open?
- ClampCurrentTransformToIntuitiveBounds (or something else) sometimes apparently leaves a thin black line
	- maybe related to above: some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
	- investigate more but one/both of these above may be related to the pan/zoom margin weirdness that is now largely better
	- repro: double monitor screenshot with 10 pixel bugger setting enabled, best fit no zoom.
	- Look at debug info: fractional pixels?
	- mismatch between ImageDisplay.Height or Width and the true ActualHeight. WPF pixel/unit nuance. At least, the black is just BG, so transparent is an option.


Maybe / ideas:
- configurable gestures: hold left and right click, then drag in a direction. 4 options. and/or middle click.
- special message or label for filename that either persists or fades after a few seconds. Configurable.
- WEBM support? Maybe a very basic kind. Certainly not a video player.
- should window do something else other than move to center of screen when changing image etc.?
- possibly other z-index shenanigans beyond always-on-top
- intuitive behavior of multiple windows
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- maybe logo for app, other name
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.
- somewhere list the less obvious features such as drag-and-drop and double-click for fullscreen
- maybe option for dropshadow beyond window ==> tried this, got messy
- option to use Zen as "background" instead of black or transparent. Then, option to zoom out beyond normal image limit (to create a frame around the image).

Features noted in Windows Photos app:
- True number displayed for "zoom amount" where 100% is true resolution, even when best-fit in small window
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