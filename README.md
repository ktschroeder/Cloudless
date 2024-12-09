# JustView

TODO
- unclickable "title" on context dropdown that shows app's nanme, maybe? I think I've seen this elsewhere before?
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- allow thru-window transparency?
- maybe logo for app, other name
- styling
- maybe zooming and panning
- ability to rotate by 90 deg increments. should it be "saved" in case user navigates to next image in directory but then back? Or in a subsequent session?
- load via drag-and-drop
- intuitive behavior of multiple windows
- optionally, recent files, where disabling it also clears the history (or just have button to clear history)
- customizable hotkeys
- graceful error handling
- support for wider range of image formats including GIFs perhaps: .jpg, .png, .bmp, .gif, .tiff, and possibly vector formats like .svg.
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.
- Clean up code, especially when "done" with everything else

advanced settings/features that can be implemented, often hidden behind flags in settings so as to keep UI clean for most people. Or consider power user mode:
- right click option to set window to typed dimensions
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- always-on-top option (global or per-image with key T), possibly other z-index shenanigans