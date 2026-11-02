using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using EasyScreenshots.Editor;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.UiTests;

public sealed class AppIconTests
{
    [AvaloniaFact]
    public void IconResourceProvidesAllTaskbarAndExplorerSizesAndEditorWindowsUseIt()
    {
        using var stream = AssetLoader.Open(new Uri("avares://EasyScreenshots/Assets/app.ico"));
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var size = reader.ReadByte(); sizes.Add(size == 0 ? 256 : size);
            reader.ReadBytes(15);
        }
        Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, sizes);
        using var pixels = new SKBitmap(20, 20);
        pixels.Erase(SKColors.White);
        using var png = pixels.Encode(SKEncodedImageFormat.Png, 100);
        var editor = new EditorWindow(png.ToArray(), () => { }, () => { });
        var picker = new CapturePickerWindow([]);
        try
        {
            Assert.NotNull(editor.Icon);
            Assert.NotNull(picker.Icon);
            Assert.Same(AppIcon.Default, editor.Icon);
            Assert.Same(AppIcon.Default, picker.Icon);
        }
        finally { editor.Close(); picker.Close(); }
    }
}
