using System.Runtime.InteropServices;
using EasyScreenshots.Platform;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.UiTests;

public sealed class CaptureTests
{
    // Each physical pixel has a unique blue/red value, so wrong origins or row strides fail.
    private static DesktopSnapshot Snapshot()
    {
        byte[] bgra =
        [
            1, 10, 20, 255, 2, 11, 21, 255, 3, 12, 22, 255,
            4, 13, 23, 255, 5, 14, 24, 255, 6, 15, 25, 255
        ];
        return new DesktopSnapshot(bgra, -3, -2, 3, 2);
    }

    [Fact]
    public void Crop_uses_physical_negative_origin_and_preserves_pixel_values()
    {
        using var snapshot = Snapshot();
        using var decoded = SKBitmap.Decode(snapshot.CropPng(-2, -2, 2, 2));
        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(new SKColor(21, 11, 2, 255), decoded.GetPixel(0, 0));
        Assert.Equal(new SKColor(25, 15, 6, 255), decoded.GetPixel(1, 1));
    }

    [Fact]
    public void Crop_clamps_to_desktop_without_rescaling_pixels()
    {
        using var snapshot = Snapshot();
        using var decoded = SKBitmap.Decode(snapshot.CropPng(-10, -8, 8, 7));
        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);
        Assert.Equal(new SKColor(20, 10, 1, 255), decoded.GetPixel(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.CropPng(20, 20, 2, 2));
    }

    [Fact]
    public void Raw_overlay_copy_honors_destination_row_padding()
    {
        using var snapshot = Snapshot();
        var destination = Marshal.AllocHGlobal(32);
        try
        {
            Marshal.Copy(Enumerable.Repeat((byte)99, 32).ToArray(), 0, destination, 32);
            snapshot.CopyPixelsTo(destination, 16);
            var actual = new byte[32];
            Marshal.Copy(destination, actual, 0, actual.Length);
            Assert.Equal(new byte[] { 1, 10, 20, 255, 2, 11, 21, 255, 3, 12, 22, 255 }, actual[..12]);
            Assert.Equal(new byte[] { 4, 13, 23, 255, 5, 14, 24, 255, 6, 15, 25, 255 }, actual[16..28]);
            Assert.All(actual[12..16], value => Assert.Equal(99, value));
            Assert.All(actual[28..32], value => Assert.Equal(99, value));
        }
        finally { Marshal.FreeHGlobal(destination); }
    }

    [Fact]
    public void Disposing_snapshot_releases_raw_pixels_and_rejects_further_access()
    {
        var snapshot = Snapshot();
        snapshot.Dispose();
        snapshot.Dispose();
        Assert.Throws<ObjectDisposedException>(() => snapshot.CropPng(-3, -2, 1, 1));
    }
}
