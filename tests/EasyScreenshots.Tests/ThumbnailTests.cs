using EasyScreenshots.Core;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.Tests;

public sealed class ThumbnailTests
{
    [Fact] public void WideImageFitsDefaultBoundsAndContainsAnnotations()
    {
        using var document = new EditorDocument(DocumentTests.Png(1000, 400));
        document.Add(new Annotation { Kind = AnnotationKind.Pen, Color = 0xffff0000, Thickness = 100, Points = [new(0, 200), new(1000, 200)] });
        using var thumbnail = SKBitmap.Decode(ImageRenderer.RenderThumbnailPng(document));
        Assert.Equal(280, thumbnail.Width);
        Assert.Equal(112, thumbnail.Height);
        Assert.True(thumbnail.GetPixel(140, 56).Green < 50);
        Assert.Single(document.Annotations);
        document.Undo(); Assert.Empty(document.Annotations);
    }

    [Fact] public void TallImageFitsHeightAndSmallImagesAreNotUpscaled()
    {
        using var tall = new EditorDocument(DocumentTests.Png(400, 1000));
        using var tallThumb = SKBitmap.Decode(ImageRenderer.RenderThumbnailPng(tall));
        Assert.Equal(68, tallThumb.Width); Assert.Equal(170, tallThumb.Height);
        using var small = new EditorDocument(DocumentTests.Png(32, 24));
        using var smallThumb = SKBitmap.Decode(ImageRenderer.RenderThumbnailPng(small));
        Assert.Equal(32, smallThumb.Width); Assert.Equal(24, smallThumb.Height);
    }

    [Fact] public void ExtremeAspectRatioHasAtLeastOnePixelAndPreviewPreservesRedo()
    {
        using var document = new EditorDocument(DocumentTests.Png(1000, 1));
        document.Add(new Annotation { Points = [new(0, 0), new(100, 0)] }); document.Undo();
        var original = document.OriginalPng;
        using var thumbnail = SKBitmap.Decode(ImageRenderer.RenderThumbnailPng(document, 15, 15));
        Assert.Equal(15, thumbnail.Width); Assert.Equal(1, thumbnail.Height);
        Assert.True(document.CanRedo); Assert.False(document.CanUndo);
        Assert.Equal(original, document.OriginalPng);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public void NonpositiveBoundsAreRejected(int width, int height)
    {
        using var document = new EditorDocument(DocumentTests.Png());
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageRenderer.RenderThumbnailPng(document, width, height));
    }
}
