using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using EasyScreenshots.Core;
using EasyScreenshots.Editor;
using SkiaSharp;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(EasyScreenshots.UiTests.TestAppBuilder))]

namespace EasyScreenshots.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public sealed class EditorInteractionTests
{
    private static byte[] Image()
    {
        using var bitmap = new SKBitmap(300, 200);
        bitmap.Erase(SKColors.White);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static void Drag(Window window, Point from, params Point[] points)
    {
        window.MouseDown(from, MouseButton.Left);
        foreach (var point in points) window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        window.MouseUp(points[^1], MouseButton.Left);
    }

    [AvaloniaFact]
    public void Rectangle_can_be_drawn_moved_resized_and_undone_one_gesture_at_a_time()
    {
        using var doc = new EditorDocument(Image());
        using var surface = new AnnotationSurface(doc) { Tool = AnnotationKind.Rectangle };
        var window = new Window { Width = 300, Height = 200, Content = surface };
        window.Show();
        try
        {
            Drag(window, new Point(20, 30), new Point(40, 45), new Point(80, 70));
            Assert.Equal(new RectD(20, 30, 60, 40), Assert.Single(doc.Annotations).Bounds);
            surface.Tool = null;
            Drag(window, new Point(30, 40), new Point(40, 45), new Point(60, 60));
            Assert.Equal(new RectD(50, 50, 60, 40), Assert.Single(doc.Annotations).Bounds);
            Drag(window, new Point(110, 90), new Point(120, 100), new Point(150, 130));
            Assert.Equal(new RectD(50, 50, 100, 80), Assert.Single(doc.Annotations).Bounds);
            doc.Undo();
            Assert.Equal(new RectD(50, 50, 60, 40), Assert.Single(doc.Annotations).Bounds);
            doc.Undo();
            Assert.Equal(new RectD(20, 30, 60, 40), Assert.Single(doc.Annotations).Bounds);
            doc.Undo();
            Assert.Empty(doc.Annotations);
            Assert.False(doc.CanUndo);
            doc.Redo();
            using var rendered = SKBitmap.Decode(ImageRenderer.RenderPng(doc));
            Assert.Equal(300, rendered.Width); Assert.Equal(200, rendered.Height);
            Assert.NotEqual(SKColors.White, rendered.GetPixel(20, 40));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Zoom_converts_pointer_coordinates_back_to_original_pixels()
    {
        using var doc = new EditorDocument(Image());
        using var surface = new AnnotationSurface(doc) { Tool = AnnotationKind.Ellipse, Zoom = 2 };
        var window = new Window { Width = 600, Height = 400, Content = surface };
        window.Show();
        try
        {
            Drag(window, new Point(40, 60), new Point(160, 140));
            Assert.Equal(new RectD(20, 30, 60, 40), Assert.Single(doc.Annotations).Bounds);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Text_click_creates_a_retained_annotation_with_selectable_bounds()
    {
        using var doc = new EditorDocument(Image());
        using var surface = new AnnotationSurface(doc) { Tool = AnnotationKind.Text, Text = "Editable\ncaption", FontSize = 20 };
        var window = new Window { Width = 300, Height = 200, Content = surface };
        window.Show();
        try
        {
            window.MouseDown(new Point(30, 25), MouseButton.Left);
            window.MouseUp(new Point(30, 25), MouseButton.Left);
            var annotation = Assert.Single(doc.Annotations);
            Assert.Equal("Editable\ncaption", annotation.Text);
            Assert.True(annotation.Bounds.Height > 40);
            surface.Tool = null;
            Drag(window, new Point(40, 40), new Point(60, 50));
            Assert.Equal(50, Assert.Single(doc.Annotations).Bounds.X);
            surface.UpdateSelected(a => a with { Text = "Changed" });
            Assert.Equal("Changed", Assert.Single(doc.Annotations).Text);
            doc.Undo();
            Assert.Equal("Editable\ncaption", Assert.Single(doc.Annotations).Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Shift_constrains_ellipse_to_a_circle()
    {
        using var doc = new EditorDocument(Image());
        using var surface = new AnnotationSurface(doc) { Tool = AnnotationKind.Ellipse };
        var window = new Window { Width = 300, Height = 200, Content = surface };
        window.Show();
        try
        {
            window.MouseDown(new Point(50, 40), MouseButton.Left);
            window.MouseMove(new Point(90, 110), RawInputModifiers.LeftMouseButton | RawInputModifiers.Shift);
            window.MouseUp(new Point(90, 110), MouseButton.Left, RawInputModifiers.Shift);
            Assert.Equal(new RectD(50, 40, 70, 70), Assert.Single(doc.Annotations).Bounds);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Editor_text_sidebar_tracks_undo_and_redo_of_selected_text()
    {
        var window = new EditorWindow(Image(), () => { }, () => { });
        window.Show();
        try
        {
            var surface = Assert.Single(window.GetVisualDescendants().OfType<AnnotationSurface>());
            var text = Assert.Single(window.GetVisualDescendants().OfType<TextBox>().Where(t => t.AcceptsReturn));
            surface.Tool = AnnotationKind.Text;
            text.Text = "Before";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var point = surface.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.NotNull(surface.Selected);
            Assert.Equal("Before", text.Text);
            text.Text = "After";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("After", surface.Selected?.Text);
            surface.Focus();
            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Before", text.Text);
            Assert.Equal("Before", surface.Selected?.Text);
            window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("After", text.Text);
        }
        finally { window.Close(); }
    }
}
