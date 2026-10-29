using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EasyScreenshots.Platform;
using SkiaSharp;

namespace EasyScreenshots.Capture;

/// <summary>Freezes the desktop once and keeps every monitor in physical pixel coordinates.</summary>
public sealed class CaptureCoordinator
{
    private bool _capturing;

    public async Task<byte[]?> CaptureAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_capturing) return null;
        _capturing = true;
        var overlays = new List<SelectionWindow>();
        DispatcherTimer? timer = null;
        try
        {
            var snapshot = DesktopCapture.Capture();
            using var bitmap = new Bitmap(new MemoryStream(snapshot.Png));
            var state = new SelectionState();
            var completion = new TaskCompletionSource<PixelRect?>(TaskCreationOptions.RunContinuationsAsynchronously);
            foreach (var monitor in Monitors())
            {
                var window = new SelectionWindow(snapshot, bitmap, monitor, state);
                window.Closed += (_, _) => completion.TrySetResult(null);
                window.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) completion.TrySetResult(null);
                };
                overlays.Add(window);
                window.Show();
                // Avalonia positions in device pixels, but dimensions are DIPs. Native sizing
                // ensures exact monitor coverage even during mixed-DPI transitions.
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle != IntPtr.Zero)
                    SetWindowPos(handle, new IntPtr(-1), monitor.X, monitor.Y, monitor.Width, monitor.Height, 0x0040);
            }
            overlays.FirstOrDefault()?.Activate();
            var wasDown = (GetAsyncKeyState(1) & 0x8000) != 0;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            timer.Tick += (_, _) =>
            {
                if ((GetAsyncKeyState(0x1B) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0)
                {
                    completion.TrySetResult(null);
                    return;
                }
                if (!GetCursorPos(out var cursor)) return;
                state.Cursor = new PixelPoint(cursor.X, cursor.Y);
                var down = (GetAsyncKeyState(1) & 0x8000) != 0;
                if (down && !wasDown)
                {
                    state.Start = state.Cursor;
                    state.Dragging = true;
                }
                if (!down && wasDown && state.Dragging)
                {
                    var selection = state.Selection;
                    if (selection.Width >= 2 && selection.Height >= 2) completion.TrySetResult(selection);
                    else state.Dragging = false;
                }
                wasDown = down;
                foreach (var overlay in overlays) overlay.Surface.InvalidateVisual();
            };
            timer.Start();
            var chosen = await completion.Task;
            timer.Stop();
            foreach (var overlay in overlays) overlay.Close();
            if (chosen is not { } bounds) return null;
            var left = Math.Clamp(bounds.X - snapshot.X, 0, snapshot.Width);
            var top = Math.Clamp(bounds.Y - snapshot.Y, 0, snapshot.Height);
            var right = Math.Clamp(bounds.Right - snapshot.X, 0, snapshot.Width);
            var bottom = Math.Clamp(bounds.Bottom - snapshot.Y, 0, snapshot.Height);
            if (right <= left || bottom <= top) return null;
            using var original = SKBitmap.Decode(snapshot.Png);
            using var crop = new SKBitmap(right - left, bottom - top, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(crop))
                canvas.DrawBitmap(original, new SKRect(left, top, right, bottom), new SKRect(0, 0, crop.Width, crop.Height));
            using var image = SKImage.FromBitmap(crop);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
        finally
        {
            timer?.Stop();
            foreach (var overlay in overlays) if (overlay.IsVisible) overlay.Close();
            _capturing = false;
        }
    }

    private static List<PixelRect> Monitors()
    {
        var result = new List<PixelRect>();
        MonitorCallback callback = (IntPtr monitor, IntPtr dc, ref NativeRect bounds, IntPtr data) =>
        {
            result.Add(new PixelRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero) || result.Count == 0)
            throw new InvalidOperationException("Windows did not report any monitors.");
        return result;
    }

    private sealed class SelectionWindow : Window
    {
        public SelectionSurface Surface { get; }
        public SelectionWindow(DesktopSnapshot snapshot, Bitmap bitmap, PixelRect monitor, SelectionState state)
        {
            Title = "Easy Screenshots · Select region";
            SystemDecorations = SystemDecorations.None;
            CanResize = false;
            ShowInTaskbar = false;
            Topmost = true;
            Background = Brushes.Black;
            Cursor = new Cursor(StandardCursorType.Cross);
            Position = new PixelPoint(monitor.X, monitor.Y);
            Width = monitor.Width;
            Height = monitor.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Content = Surface = new SelectionSurface(snapshot, bitmap, monitor, state);
        }
    }

    private sealed class SelectionState
    {
        public PixelPoint Cursor, Start;
        public bool Dragging;
        public PixelRect Selection => new(Math.Min(Start.X, Cursor.X), Math.Min(Start.Y, Cursor.Y),
            Math.Abs(Start.X - Cursor.X), Math.Abs(Start.Y - Cursor.Y));
    }

    private sealed class SelectionSurface(DesktopSnapshot snapshot, Bitmap bitmap, PixelRect monitor, SelectionState state) : Control
    {
        public override void Render(DrawingContext context)
        {
            var destination = new Rect(Bounds.Size);
            var source = new Rect(monitor.X - snapshot.X, monitor.Y - snapshot.Y, monitor.Width, monitor.Height);
            context.DrawImage(bitmap, source, destination);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(145, 0, 0, 0)), destination);
            var sx = Bounds.Width / monitor.Width;
            var sy = Bounds.Height / monitor.Height;
            var selection = state.Selection;
            var local = new Rect((selection.X - monitor.X) * sx, (selection.Y - monitor.Y) * sy,
                selection.Width * sx, selection.Height * sy);
            if (state.Dragging && selection.Width > 0 && selection.Height > 0)
            {
                using (context.PushClip(local)) context.DrawImage(bitmap, source, destination);
                context.DrawRectangle(null, new Pen(Brushes.White, 1), local);
            }
            var cursor = new Point((state.Cursor.X - monitor.X) * sx, (state.Cursor.Y - monitor.Y) * sy);
            if (destination.Contains(cursor))
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1);
                context.DrawLine(pen, new Point(0, cursor.Y), new Point(Bounds.Width, cursor.Y));
                context.DrawLine(pen, new Point(cursor.X, 0), new Point(cursor.X, Bounds.Height));
            }
            var label = state.Dragging
                ? $"{selection.Width} × {selection.Height} px   •   Release to capture   •   Esc to cancel"
                : "Drag to capture   •   Esc or right-click to cancel";
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 14, Brushes.White);
            var labelRect = new Rect(Math.Max(12, (Bounds.Width - text.Width) / 2 - 16), 24, text.Width + 32, 42);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(230, 25, 28, 35)), null, labelRect, 10, 10);
            context.DrawText(text, new Point(labelRect.X + 16, labelRect.Y + 11));
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref NativeRect bounds, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
