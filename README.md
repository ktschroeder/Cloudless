# JustView

TODO
- like oter Windows apps, maximize vertical dimension when dragging window top to top of screen. And/or have hotkey for maximizing vertical space.
- option: snap to best fit
- webp looks worse on this than windows photo viewer. anti-aliasing or something? maybe conversion to BMP or not leveraging benefits of WEBP. Also ImageSharp looks unnecessarily heavy for our purposes and is large.
- - implement: holdintg ctrl or shift while dragging corner/side fo window keeps ratio
..- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- allow thru-window transparency? option to show transparency as black, or something else (gray checker?), or thru-window. ==> possibility of appearance of entirely floating image, would be neat.
- maybe option for dropshadow beyond window
- maybe logo for app, other name
- styling
- maybe zooming and panning
- ability to rotate by 90 deg increments. should it be "saved" in case user navigates to next image in directory but then back? Or in a subsequent session?
- intuitive behavior of multiple windows
- optionally, recent files, where disabling it also clears the history (or just have button to clear history)
- customizable hotkeys
- graceful error handling
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.
- Clean up code, especially when "done" with everything else

advanced settings/features that can be implemented, often hidden behind flags in settings so as to keep UI clean for most people. Or consider power user mode:
- right click option to set window to typed dimensions
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- always-on-top option (global or per-image with key T), possibly other z-index shenanigans