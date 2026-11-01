using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using EasyScreenshots.Core;
using EasyScreenshots.Editor;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.UiTests;

public sealed class ThumbnailCacheTests
{
    [AvaloniaFact]
    public void EditorThumbnailRefreshesAfterEditAndUndoWithoutChangingHistory()
    {
        using var pixels = new SKBitmap(300, 200);
        pixels.Erase(SKColors.White);
        using var encoded = pixels.Encode(SKEncodedImageFormat.Png, 100);
        var editor = new EditorWindow(encoded.ToArray(), () => { }, () => { });
        editor.Show();
        try
        {
            var original = editor.GetThumbnailPng();
            var surface = Assert.Single(editor.GetVisualDescendants().OfType<AnnotationSurface>());
            surface.Tool = AnnotationKind.Text;
            surface.Text = "Marked";
            var point = surface.TranslatePoint(new Point(30, 30), editor)!.Value;
            editor.MouseDown(point, MouseButton.Left);
            editor.MouseUp(point, MouseButton.Left);
            var edited = editor.GetThumbnailPng();
            Assert.False(original.SequenceEqual(edited));
            editor.Hide();
            editor.Show();
            Assert.Equal(edited, editor.GetThumbnailPng());
            surface.Focus();
            editor.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            editor.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Equal(original, editor.GetThumbnailPng());
            editor.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            editor.KeyReleaseQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            Assert.Equal(edited, editor.GetThumbnailPng());
        }
        finally { editor.Close(); }
    }
}
