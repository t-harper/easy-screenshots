# Easy Screenshots

Windows 11 screenshot capture and markup, built with C# / Avalonia. Portable win-x64 application; no installer or administrator rights required.

## Run

Run `artifacts/portable/EasyScreenshots.exe`. The portable build includes .NET and native libraries; its first launch extracts bundled native components into the user's temporary directory. Keep the executable at a stable location when using sign-in startup.

- **Ctrl+Shift+4** globally: dim the desktop, drag a region, release to open the editor. **Escape** or right-click cancels. The mouse pointer is excluded. Multiple monitors use physical pixel coordinates.
- Left toolbar: Select/move, arrow, text, pen, highlighter, rectangle, ellipse and blur. Choose color, thickness and text/font size. Annotations remain separate objects until PNG export.
- **V**: Select mode. Select an annotation to move it; drag its blue bottom-right corner to resize. Hold **Shift** while drawing rectangles/ellipses for squares/circles. Edit selected text in the left panel. **Delete** removes selected annotations.
- **Ctrl+Z**, **Ctrl+Y** or **Ctrl+Shift+Z**: undo/redo.
- **Ctrl+S**: PNG Save As. **Drag image**: drag a rendered PNG into Explorer or a compatible application. Terminal behavior depends on whether the destination accepts file drops or paths.
- Tray menu: New capture, reopen last capture, main window, options and exit. Home's **Reopen captures** restores all retained editors. Closing windows keeps documents in memory when close-to-tray is enabled; Exit ends the app and discards editor sessions.
- Options: close-to-tray and Windows sign-in startup. Startup defaults off. Settings are saved under `%LOCALAPPDATA%/EasyScreenshots/settings.json`; enabling startup adds a current-user Windows Run entry. Disabling it removes the entry.

Exported drag files remain in `%LOCALAPPDATA%/EasyScreenshots/DragExports` so receiving applications can use them after the drag completes. They are not removed automatically; remove old exports when no longer needed.

If the shortcut is owned by another app, Easy Screenshots displays the conflict. Release it in the other app and restart Easy Screenshots. The app runs one instance per Windows session.

## Build and test

Requires Windows 11 x64 and .NET SDK 9.0.320 or a compatible .NET 9 SDK. NuGet access is needed for first restore.

```powershell
./build.ps1 Build
./build.ps1 Test
./build.ps1 Publish
```

Or open `EasyScreenshots.sln` in Visual Studio/Rider. Code is split into platform capture, Avalonia editor and a UI-independent Core. Core tests exercise history, geometry, all render tools, blur layering, invalid input and settings. Headless UI tests exercise pointer-driven editing. Native checks can be run while the app is stopped:

```powershell
$process = Start-Process ./artifacts/portable/EasyScreenshots.exe -ArgumentList '--native-smoke', "$PWD/artifacts/native-smoke.json" -Wait -PassThru
Get-Content ./artifacts/native-smoke.json
```

The native check registers/releases the real shortcut and captures/decodes the desktop in memory; it saves only the JSON result, no screenshot. Live multi-monitor mixed-DPI selection and drag into each destination app require interactive validation. Protected video, secure desktops and HDR display color may be subject to Windows capture limitations.
