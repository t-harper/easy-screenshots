# Easy Screenshots Implementation Plan

Goal: build a portable Windows 11 capture and annotation app using Avalonia.

## Parallel ownership

- Core agent: src/EasyScreenshots.Core/** and tests/EasyScreenshots.Tests/**. Geometry, retained annotations, undo/redo, Skia PNG export and behavioral tests.
- Windows agent: src/EasyScreenshots/Platform/** and Capture/**. Native global hotkey, snapshot and physical-coordinate selection overlays.
- Editor agent: src/EasyScreenshots/Editor/**. Dark editor, retained annotation interaction, saving and drag-out.
- Coordinator: project setup, App shell, tray/settings/startup, integration, review and publish.

## Interfaces (binding)

Core namespace EasyScreenshots.Core. PointD(double X,double Y), RectD(double X,double Y,double Width,double Height) with FromPoints, Contains, Intersect. AnnotationKind: Arrow,Text,Rectangle,Ellipse,Pen,Highlighter,Blur. Annotation record properties Id(Guid), Kind, Points(PointD[]), Color(uint ARGB), Thickness(double), Text(string), FontSize(double). Bounds property; Translate(dx,dy), Resize(RectD target). EditorDocument(byte[] png), Width/Height int, OriginalPng byte[], Annotations IReadOnlyList<Annotation>, Add/Replace/Remove, Undo/Redo, CanUndo/CanRedo, Changed event EventHandler. ImageRenderer.RenderPng(EditorDocument) byte[].

Windows integration: DesktopSnapshot(byte[] Png,int X,int Y,int Width,int Height); DesktopCapture.Capture() static. GlobalHotkey(Action callback) IDisposable, constructor registers Ctrl+Shift+4 or throws. CaptureCoordinator.CaptureAsync() Task<byte[]?> handles capture, overlays, cropping and cancellation.

Editor: EditorWindow(byte[] png, Action requestCapture, Action requestOptions) : Avalonia.Controls.Window. Closing managed by App. Expose no other required interface.

## Steps

- [x] Scaffold projects, pin dependencies, restore.
- [x] Implement behavioral tests; verify regressions fail before fixes and pass afterwards. Initial Core test execution was blocked by sandbox restore/testhost access until elevated execution.
- [x] Implement independent Windows integration and editor against binding interfaces.
- [x] Integrate shell, tray, options and startup; test settings persistence.
- [x] Review complete code, fix integration/build findings, run all tests.
- [x] Publish win-x64 self-contained portable executable and document run/build/test instructions.

Review focus: mixed DPI/negative origins; cancel and repeated shortcut; editable blur/export consistency; undo after moves and resize; drag files remain available; unavailable shortcut is visible.
