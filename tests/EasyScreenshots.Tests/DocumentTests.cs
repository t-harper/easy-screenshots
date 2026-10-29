using EasyScreenshots.Core;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.Tests;

public class DocumentTests
{
    public static byte[] Png(int width = 32, int height = 24, bool checker = false)
    {
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            bitmap.SetPixel(x, y, checker && (x + y) % 2 == 0 ? SKColors.Black : SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact] public void ReverseDragAndNegativeOriginsNormalizeAndClip()
    {
        var bounds = RectD.FromPoints(new(-10, 20), new(-80, -30));
        Assert.Equal(new RectD(-80, -30, 70, 50), bounds);
        Assert.Equal(new RectD(-40, 0, 30, 20), bounds.Intersect(new(-40, 0, 100, 100)));
        Assert.True(bounds.Contains(new(-20, 10)));
        Assert.Equal(0, bounds.Intersect(new(200, 200, 10, 10)).Width);
    }

    [Fact] public void HistoryRestoresMovesAndDeletionAndClearsRedoBranch()
    {
        var doc = new EditorDocument(Png());
        var annotation = new Annotation { Kind = AnnotationKind.Rectangle, Points = [new(2, 3), new(12, 13)] };
        doc.Add(annotation);
        doc.Replace(annotation.Translate(8, 9));
        doc.Remove(annotation.Id);
        Assert.Empty(doc.Annotations);
        doc.Undo(); Assert.Equal(new RectD(10, 12, 10, 10), doc.Annotations[0].Bounds);
        doc.Undo(); Assert.Equal(annotation.Bounds, doc.Annotations[0].Bounds);
        doc.Redo(); Assert.Equal(10, doc.Annotations[0].Bounds.X);
        doc.Add(new Annotation()); Assert.False(doc.CanRedo);
    }

    [Fact] public void HistoryOwnsSnapshotsInsteadOfExternalPointArrays()
    {
        var doc = new EditorDocument(Png());
        var points = new[] { new PointD(1, 2), new PointD(8, 9) };
        doc.Add(new Annotation { Points = points });
        points[0] = new(999, 999);
        Assert.Equal(new PointD(1, 2), doc.Annotations[0].Points[0]);
        var exposed = doc.Annotations[0].Points; exposed[0] = new(999, 999);
        Assert.Equal(new PointD(1, 2), doc.Annotations[0].Points[0]);
    }

    [Fact] public void ResizeScalesRetainedPointsAndTranslatePreservesShape()
    {
        var a = new Annotation { Kind = AnnotationKind.Pen, Points = [new(1, 2), new(6, 7), new(11, 12)] };
        var resized = a.Resize(new(20, 30, 40, 60));
        Assert.Equal(new PointD(40, 60), resized.Points[1]);
        Assert.Equal(new RectD(22, 33, 40, 60), resized.Translate(2, 3).Bounds);
    }

    [Fact] public void ExportKeepsPixelDimensionsAndRendersAnnotations()
    {
        var doc = new EditorDocument(Png());
        doc.Add(new Annotation { Kind = AnnotationKind.Rectangle, Color = 0xffff0000, Thickness = 2, Points = [new(5, 5), new(25, 18)] });
        using var result = SKBitmap.Decode(ImageRenderer.RenderPng(doc));
        Assert.Equal(32, result.Width); Assert.Equal(24, result.Height);
        Assert.True(result.GetPixel(5, 10).Red > 200);
        Assert.True(result.GetPixel(5, 10).Green < 80);
        Assert.Equal(SKColors.White, result.GetPixel(30, 22));
    }

    [Fact] public void BlurChangesOnlySelectedRegionAndUndoRestoresOriginal()
    {
        var png = Png(checker: true); var doc = new EditorDocument(png);
        doc.Add(new Annotation { Kind = AnnotationKind.Blur, Points = [new(8, 8), new(24, 20)], Thickness = 8 });
        using var original = SKBitmap.Decode(png);
        using var blurred = SKBitmap.Decode(ImageRenderer.RenderPng(doc));
        Assert.Equal(original.GetPixel(2, 2), blurred.GetPixel(2, 2));
        Assert.InRange((int)blurred.GetPixel(16, 12).Red, 30, 225);
        doc.Undo();
        using var undone = SKBitmap.Decode(ImageRenderer.RenderPng(doc));
        Assert.Equal(original.GetPixel(16, 12), undone.GetPixel(16, 12));
    }

    [Fact] public void InvalidImagesAreRejected() => Assert.Throws<ArgumentException>(() => new EditorDocument([1, 2, 3]));

    [Fact] public void MultilineTextBoundsContainEveryLine()
    {
        var annotation = new Annotation { Kind = AnnotationKind.Text, Points = [new(4, 5)], FontSize = 10, Text = "a\nlongest\na" };
        Assert.True(annotation.Bounds.Width >= 7 * 6.5);
        Assert.True(annotation.Bounds.Height >= 36);
    }

    [Theory]
    [InlineData(AnnotationKind.Arrow)]
    [InlineData(AnnotationKind.Ellipse)]
    [InlineData(AnnotationKind.Pen)]
    [InlineData(AnnotationKind.Highlighter)]
    [InlineData(AnnotationKind.Text)]
    public void EveryVisibleToolChangesImage(AnnotationKind kind)
    {
        var doc = new EditorDocument(Png(100, 100));
        doc.Add(new Annotation { Kind = kind, Color = 0xff0000ff, Points = [new(20, 20), new(70, 70)], Text = "Hi", FontSize = 22 });
        using var result = SKBitmap.Decode(ImageRenderer.RenderPng(doc));
        Assert.Contains(result.Pixels, pixel => pixel.Red < 250);
    }

    [Fact] public void HighlighterIsTranslucentAndUndoRedoIsLossless()
    {
        var doc = new EditorDocument(Png());
        doc.Add(new Annotation { Kind = AnnotationKind.Highlighter, Color = 0xffff0000, Points = [new(2, 10), new(28, 10)] });
        var rendered = ImageRenderer.RenderPng(doc);
        using var bitmap = SKBitmap.Decode(rendered);
        Assert.InRange((int)bitmap.GetPixel(15, 10).Green, 100, 230);
        doc.Undo(); doc.Redo();
        Assert.Equal(rendered, ImageRenderer.RenderPng(doc));
    }

    [Fact] public void TextResizeScalesFontAndKeepsAnchor()
    {
        var text = new Annotation { Kind = AnnotationKind.Text, Text = "hello", FontSize = 20, Points = [new(10, 10)] };
        var resized = text.Resize(new(30, 40, text.Bounds.Width * 2, text.Bounds.Height * 2));
        Assert.Equal(40, resized.FontSize);
        Assert.Equal(new PointD(30, 40), resized.Points[0]);
        Assert.Equal(text.Bounds.Width * 2, resized.Bounds.Width);
    }

    [Fact] public void BlurRespectsAnnotationLayerOrder()
    {
        var stroke = new Annotation { Kind = AnnotationKind.Pen, Color = 0xff000000, Thickness = 2, Points = [new(2, 12), new(30, 12)] };
        var blur = new Annotation { Kind = AnnotationKind.Blur, Thickness = 4, Points = [new(4, 4), new(28, 22)] };
        var below = new EditorDocument(Png()); below.Add(stroke); below.Add(blur);
        var above = new EditorDocument(Png()); above.Add(blur); above.Add(stroke);
        using var softened = SKBitmap.Decode(ImageRenderer.RenderPng(below));
        using var sharp = SKBitmap.Decode(ImageRenderer.RenderPng(above));
        Assert.True(softened.GetPixel(16, 12).Red > 100);
        Assert.True(sharp.GetPixel(16, 12).Red < 30);
    }

    [Fact] public void DraftPreviewReplacesLayerInPlaceWithoutEditingDocument()
    {
        var doc = new EditorDocument(Png());
        var stroke = new Annotation { Kind = AnnotationKind.Pen, Color = 0xff000000, Thickness = 2, Points = [new(2, 12), new(30, 12)] };
        doc.Add(stroke);
        doc.Add(new Annotation { Kind = AnnotationKind.Blur, Thickness = 4, Points = [new(4, 4), new(28, 22)] });
        doc.Undo(); doc.Redo();
        using var result = ImageRenderer.RenderBitmap(doc, stroke with { Color = 0xff0000ff });
        Assert.Equal(SKColorType.Bgra8888, result.ColorType);
        Assert.Equal(SKAlphaType.Premul, result.AlphaType);
        Assert.True(result.GetPixel(16, 12).Red > 100); // Blur must remain above replaced stroke.
        Assert.Equal(0xff000000u, doc.Annotations[0].Color);
        Assert.True(doc.CanUndo); Assert.False(doc.CanRedo);
        doc.Undo(); Assert.Single(doc.Annotations); // Preview did not add a history entry.
    }

    [Fact] public void DraftPreviewAppendsNewLayerAboveExistingLayers()
    {
        var doc = new EditorDocument(Png());
        doc.Add(new Annotation { Kind = AnnotationKind.Blur, Thickness = 4, Points = [new(4, 4), new(28, 22)] });
        var draft = new Annotation { Kind = AnnotationKind.Pen, Color = 0xff000000, Thickness = 2, Points = [new(2, 12), new(30, 12)] };
        using var result = ImageRenderer.RenderBitmap(doc, draft);
        Assert.True(result.GetPixel(16, 12).Red < 30);
        Assert.Single(doc.Annotations);
        doc.Undo();
        using var redoPreview = ImageRenderer.RenderBitmap(doc, draft);
        Assert.Empty(doc.Annotations);
        Assert.True(doc.CanRedo); Assert.False(doc.CanUndo);
    }

    [Fact] public void DocumentOwnsOriginalBytesAndDisposesCachedImageSafely()
    {
        var bytes = Png();
        var doc = new EditorDocument(bytes);
        bytes[0] = 0;
        var exposed = doc.OriginalPng; exposed[0] = 0;
        using (var preview = ImageRenderer.RenderBitmap(doc)) Assert.Equal(SKColors.White, preview.GetPixel(10, 10));
        doc.Dispose(); doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ImageRenderer.RenderBitmap(doc));
    }

    [Fact] public void SettingsRoundTripAndCorruptFallback()
    {
        var folder = Path.Combine(Path.GetTempPath(), "easy-screenshots-tests-" + Guid.NewGuid());
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Assert.Equal(new AppSettings(), SettingsStore.Load(path));
            SettingsStore.Save(path, new(false, true));
            Assert.Equal(new AppSettings(false, true), SettingsStore.Load(path));
            SettingsStore.Save(path, new(true, false));
            Assert.Equal(new AppSettings(true, false), SettingsStore.Load(path));
            File.WriteAllText(path, "{bad json");
            Assert.Equal(new AppSettings(), SettingsStore.Load(path));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
