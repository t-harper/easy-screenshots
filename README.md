# Easy Screenshots

A lightweight screenshot and markup app for Windows 11, built with Avalonia and C#.

Press **Ctrl+Shift+4** or **Win+Shift+4**, drag to select an area, and annotate it in a dark editor. Save a PNG or drag the finished image into a folder or another app.

## Features

- Global capture shortcut with a dimmed selection overlay and crosshair.
- Multi-monitor capture without the mouse pointer.
- Arrows, text, freehand pen, highlighter, rectangles, ellipses, and blur.
- Select, move, resize, and recolor annotations; adjust thickness and font size.
- Undo/redo, zoom, PNG Save As, and a dedicated **Drag image** control.
- System tray access, optional sign-in startup, and close-to-tray settings.
- Portable, self-contained Windows x64 executable.

## Build and run

### Download

Download [EasyScreenshots.exe for Windows 11 x64](https://github.com/t-harper/easy-screenshots/releases/latest/download/EasyScreenshots.exe), then run it. No installer or .NET installation is needed. Release assets also include license notices and a SHA-256 checksum.

### Build from source

Requires **Windows 11 x64** and **.NET SDK 9.0.320 or a later .NET 9 feature band**. The first build downloads dependencies from NuGet.

```powershell
git clone https://github.com/t-harper/easy-screenshots.git
cd easy-screenshots
./build.ps1 Build
```

Run from source:

```powershell
dotnet run --project src/EasyScreenshots
```

Create and run the portable executable:

```powershell
./build.ps1 Publish
./artifacts/portable/EasyScreenshots.exe
```

The published app includes .NET and its native dependencies; users do not need to install the runtime. On first launch, bundled native libraries are extracted to a temporary directory. Exit any running copy before replacing its executable.

## Capture and edit

1. Launch the app, then press **Ctrl+Shift+4** or **Win+Shift+4** from anywhere.
2. Drag across the area to capture. Release to open the editor, or press **Escape** or right-click to cancel.
3. Choose a tool from the left toolbar and draw on the screenshot.
4. Switch to **Select / move** to reposition annotations. Drag the blue bottom-right handle to resize a selected annotation. Edit selected text in the left panel.
5. Press **Ctrl+S** to save a PNG, or drag **Drag image** into a folder or an app that accepts files.

Hold **Shift** while drawing rectangles or ellipses to make squares or circles.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+Shift+4 | Start a capture globally |
| Win+Shift+4 | Start a capture globally; overrides the taskbar shortcut while the app runs |
| Escape | Cancel capture or return to selection mode in the editor |
| V | Select / move tool |
| Delete | Delete the selected annotation |
| Ctrl+Z | Undo |
| Ctrl+Y or Ctrl+Shift+Z | Redo |
| Ctrl+S | Save As PNG |

Editing shortcuts apply in the editor; text fields keep their normal text-editing behavior.

## Tray and settings

The tray menu provides capture, options, **Reopen captures**, and **Exit**. **Reopen captures** opens a thumbnail picker in newest-first order. Each card shows the current annotated image, capture time, and dimensions. Select a card to reopen only that editor, preserving its annotations and undo history. The picker lists captures retained in the current app session; it does not scan saved files or restore sessions after exit.

Close-to-tray is enabled by default. Closing an editor keeps it in memory; exiting the app discards unsaved sessions. Sign-in startup is optional and disabled by default. Keep the portable executable in a permanent location before enabling it.

Settings are stored in `%LOCALAPPDATA%\EasyScreenshots\settings.json`. Sign-in startup uses the current user's Windows Run registry entry.

Dragging exports a PNG to `%LOCALAPPDATA%\EasyScreenshots\DragExports`. These files remain available for receiving applications and can be removed manually when no longer needed. File-drop behavior in terminals depends on the destination application.

## Tests

```powershell
./build.ps1 Test
```

Core tests cover annotation history, geometry, rendering, blur layering, arrow styling, and settings. Headless Avalonia tests cover drawing, moving, resizing, zoom, and text editing with undo/redo.

For a native capture and shortcut check, exit the app first, then run:

```powershell
$app = Join-Path $PWD 'artifacts/portable/EasyScreenshots.exe'
$report = Join-Path $PWD 'artifacts/native-smoke.json'
Start-Process -FilePath $app -ArgumentList @('--native-smoke', "`"$report`"") -Wait
Get-Content $report
```

The native check captures the desktop in memory and writes only a JSON report. Multi-monitor selection with different display scaling and drag-out into individual applications also need interactive testing.

## Troubleshooting

- **Shortcut unavailable:** another app may own Ctrl+Shift+4. Release the shortcut there and restart Easy Screenshots.
- **App already running:** only one instance runs per Windows session. Open it from the tray.
- **Cannot overwrite the executable:** exit the running app from the tray before publishing or replacing it.
- **Capture differs from the display:** protected content, secure desktops, and HDR can be subject to Windows capture limitations.

## License

[MIT](LICENSE). Use, modify, redistribute, or sell the software; retain the copyright and license notice. Third-party dependencies retain their respective licenses.
