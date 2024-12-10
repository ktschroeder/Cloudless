# JustView

TODO

Probably should/will:
- some images leave a small black bar on window, maybe off-by-one from math/rounding. see double-monitor screencap as example of an image that does this.
- maybe zooming and panning. ctrl scroll to zoom or ctrl and plus/minus. ctrl 0 and 9 for window fit and true resolution (100%). ctrl click and drag to pan. (click and drag moves window; maybe option to swap which needs CTRL)
- optionally, recent files, where disabling it also clears the history (or just have button to clear history). Windows natively provides a history function when right clicking on icon on taskbar. Manage and given option to enable/disable this?
- odd jitter of window when changing images, such as when navigating through a directory


Probably should but lower priority:
- loading GIF takes a while, may be good to show "Loading..." text or something. User cursor stays weird? Possibly blocks stuff?
- styling
- right click option to set window to typed dimensions
- customizable hotkeys
- graceful error handling
- Clean up code, especially when "done" with everything else
- option: snap to best fit when dragging border
- allow thru-window transparency? option to show transparency as black, or something else (gray checker? see what other apps do), or thru-window. ==> possibility of appearance of entirely floating image, would be neat.
- consider whether/what features should be behind feature flags users can configure in settings (to keep app/UI cleaner if they like). Also consider some kind of power user mode for this.
- implement: holding ctrl while dragging corner of window keeps ratio. This actually has some complications, such as where the window should be "pinned". See what other apps do for intuitive UX.
- If you change a config in one window/instance, how should/can it affec tother windows that are open?

Maybe / ideas:
- per-window/instance always-on-top via a hotkey like "T".
- should window do something else other than move to center of screen when changing image etc.?
- possibly other z-index shenanigans beyond always-on-top
- intuitive behavior of multiple windows
- (maybe difficult) smart colliding/snapping with other windows/instances of the app
- maybe logo for app, other name
- maybe option for dropshadow beyond window
- Automatically rotate images based on the EXIF orientation tag in case the image was taken on a camera or smartphone that stored the rotation information.