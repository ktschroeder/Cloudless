# Testing

## Automated tests

The test project targets `net8.0-windows` because the application uses WPF. Run it on Windows with the .NET 8 SDK:

```powershell
dotnet test Cloudless.Tests/Cloudless.Tests.csproj --configuration Debug
dotnet test Cloudless.Tests/Cloudless.Tests.csproj --configuration Release
```

GitHub Actions runs both configurations on Windows for pushes and pull requests. The suite covers file-type classification, crop-selection invariants and coordinate conversion, viewport resizing/zoom/pan calculations, tag persistence/query/autocomplete behavior, bookmark persistence, workspace file save/load and JSON compatibility, page-token navigation, command video-delay parsing, video-position persistence, closed-window history, slideshow progression/trigger/shuffle behavior, preload and cache-retention index boundaries, plugin discovery/assembly enumeration, and GIF/WebP animation detection. Tests use temporary files for persistence and media-format cases. They do not launch WPF windows or VLC.

## Manual smoke checks

Run these on Windows with the release build and VLC plugin installed when testing video behavior:

- Open a directory with supported images and videos; move between files and confirm the displayed media matches the selection.
- Select, crop, zoom, and pan an image; resize the window and confirm the view and crop remain usable.
- Save a workspace containing multiple pages, windows, and a video with loop/volume state; close and restore it, then check the restored state.
- Run a slideshow with and without triggers, pause/resume it, and verify it stops cleanly.
- Open and control a video (play/pause, seek, mute/volume, loop); close it and confirm image navigation still works.
- Exercise the command palette and keyboard shortcuts, including invalid input, and confirm the app remains responsive.
- Open a large directory and observe initial load, scrolling, and memory behavior.
