# Easy Screenshots

Approved direction: Windows 11, C# and Avalonia, portable deployment. Global Ctrl+Shift+4 captures a frozen desktop selection across monitors without the cursor. Escape cancels. A dark editor has a left toolbar, retained selectable/movable/resizable annotations (arrow, text, rectangle, ellipse, pen, highlighter, blur), color/thickness controls and undo/redo. Ctrl+S opens PNG Save As. Drag image exports a PNG file for Explorer and compatible terminals. Tray options control close-to-tray and sign-in startup. No automatic screenshot saving.

Architecture: a UI-independent Core owns image-pixel coordinates, immutable annotation records, history and Skia PNG rendering. Avalonia owns editor and selection interaction. Win32 owns physical-pixel virtual-desktop capture and RegisterHotKey. A single desktop snapshot is displayed in one overlay per monitor; selection uses physical coordinates, including negative origins and mixed DPI. Temporary drag files live in local app data and persist long enough for receiving applications.

Alternatives considered: WPF (mature Windows-specific tooling), WinUI 3 (modern native controls), Avalonia (user-selected). Framework choice is Avalonia; Windows integration remains native Win32. Shortcut registration failure is surfaced rather than silently claiming ownership.

Validation: unit tests exercise history, geometry, image rendering/blur and settings. Build and self-contained publish are required. Live UI checks are reported separately from automated checks.
