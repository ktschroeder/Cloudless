# JustView

TODO

Probably should/will:
- encountered before, some bug with viewing image details maybe when image is dragged in from file explorer or from web.
- like other Windows apps, maximize vertical dimension when dragging window top to top of screen. And/or have hotkey for maximizing vertical space.
- implement: holdintg ctrl or shift while dragging corner/side fo window keeps ratio
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- maybe zooming and panning. ctrl scroll to zoom or ctrl and plus/minus. ctrl click and drag to pan. (click and drag moves window; maybe option to swap which needs CTRL)
- ability to rotate by 90 deg increments. should it be "saved" in case user navigates to next image in directory but then back? Or in a subsequent session?
- always-on-top option (global or per-image with key T)
- optionally, recent files, where disabling it also clears the history (or just have button to clear history). Windows natively provides a history function when right clicking on icon on taskbar. Manage and given option to enable/disable this?
- for GIFs: can have RepeatBehavior (whether to loop) be a config


Probably should but lower priority:
- styling
- right click option to set window to typed dimensions
- customizable hotkeys
- graceful error handling
- Clean up code, especially when "done" with everything else
- option: snap to best fit when dragging border
- allow thru-window transparency? option to show transparency as black, or something else (gray checker? see what other apps do), or thru-window. ==> possibility of appearance of entirely floating image, would be neat.
- consider whether/what features should be behind feature flags users can configure in settings (to keep app/UI cleaner if they like). Also consider some kind of power user mode for this.


Maybe / ideas:
- possibly other z-index shenanigans beyond always-on-top
- intuitive behavior of multiple windows
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- maybe logo for app, other name
- maybe option for dropshadow beyond window
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.