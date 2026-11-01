using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace EasyScreenshots.Platform;

/// <summary>A top-down, opaque BGRA desktop frame. PNG encoding is deferred until export.</summary>
public sealed class DesktopSnapshot : IDisposable
{
    private byte[]? _pixels;
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int RowBytes => checked(Width * 4);

    public DesktopSnapshot(byte[] bgraPixels, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (width <= 0 || height <= 0 || bgraPixels.Length != checked(width * height * 4))
            throw new ArgumentException("The buffer must contain one BGRA pixel per desktop pixel.", nameof(bgraPixels));
        _pixels = bgraPixels;
        X = x; Y = y; Width = width; Height = height;
    }

    // Compatibility for diagnostics. The interactive capture path never encodes the whole desktop.
    public byte[] Png => CropPng(X, Y, Width, Height);

    public void CopyPixelsTo(IntPtr destination, int rowBytes)
    {
        var pixels = _pixels ?? throw new ObjectDisposedException(nameof(DesktopSnapshot));
        if (destination == IntPtr.Zero) throw new ArgumentException("A destination buffer is required.", nameof(destination));
        if (rowBytes < RowBytes) throw new ArgumentOutOfRangeException(nameof(rowBytes));
        if (rowBytes == RowBytes)
            Marshal.Copy(pixels, 0, destination, pixels.Length);
        else
            for (var row = 0; row < Height; row++)
                Marshal.Copy(pixels, row * RowBytes, IntPtr.Add(destination, checked(row * rowBytes)), RowBytes);
    }

    public byte[] CropPng(int x, int y, int width, int height)
    {
        var pixels = _pixels ?? throw new ObjectDisposedException(nameof(DesktopSnapshot));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var left = (int)Math.Clamp((long)x - X, 0, Width);
        var top = (int)Math.Clamp((long)y - Y, 0, Height);
        var right = (int)Math.Clamp((long)x + width - X, 0, Width);
        var bottom = (int)Math.Clamp((long)y + height - Y, 0, Height);
        if (right <= left || bottom <= top) throw new ArgumentOutOfRangeException(nameof(x), "Selection does not intersect the desktop.");
        using var crop = new SKBitmap(right - left, bottom - top, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (var row = 0; row < crop.Height; row++)
            Marshal.Copy(pixels, checked((top + row) * RowBytes + left * 4),
                IntPtr.Add(crop.GetPixels(), checked(row * crop.RowBytes)), checked(crop.Width * 4));
        using var image = SKImage.FromBitmap(crop);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    public void Dispose() => _pixels = null;
}

public static class DesktopCapture
{
    public static DesktopSnapshot Capture()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows 11 is required.");
        var x = GetSystemMetrics(76);
        var y = GetSystemMetrics(77);
        var width = GetSystemMetrics(78);
        var height = GetSystemMetrics(79);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("No desktop is available to capture.");
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw Error("Cannot access the desktop.");
        var memoryDc = IntPtr.Zero;
        var dib = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) throw Error("Cannot create capture buffer.");
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            dib = CreateDIBSection(screenDc, ref info, 0, out var pixels, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) throw Error("Cannot allocate capture bitmap.");
            previous = SelectObject(memoryDc, dib);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw Error("Cannot select capture bitmap.");
            // SRCCOPY | CAPTUREBLT captures layered windows, but never draws the cursor.
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, 0x40CC0020))
                throw Error("Windows could not capture the desktop.");
            var bytes = new byte[checked(width * height * 4)];
            Marshal.Copy(pixels, bytes, 0, bytes.Length);
            // GDI's reserved alpha byte is undefined. PNG must be fully opaque.
            for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            return new DesktopSnapshot(bytes, x, y, width, height);
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memoryDc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static Win32Exception Error(string message) => new(Marshal.GetLastWin32Error(), message);
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
}
