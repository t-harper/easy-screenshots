using EasyScreenshots.Core;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.Tests;

public sealed class ArrowStyleTests
{
    [Fact]
    public void ArrowHeadHasSolidInteriorBesideShaft()
    {
        using var document = new EditorDocument(DocumentTests.Png(160, 120));
        document.Add(new Annotation { Kind = AnnotationKind.Arrow, Color = 0xff007aff, Thickness = 6, Points = [new(20, 60), new(140, 60)] });
        using var image = ImageRenderer.RenderBitmap(document);
        var interior = image.GetPixel(119, 67);
        Assert.True(interior.Blue > 220 && interior.Red < 50);
        Assert.Equal(SKColors.White, image.GetPixel(100, 75));
    }

    [Fact]
    public void ShortThickArrowDoesNotGrowHeadBehindItsTail()
    {
        using var document = new EditorDocument(DocumentTests.Png(80, 80));
        document.Add(new Annotation { Kind = AnnotationKind.Arrow, Color = 0xff007aff, Thickness = 12, Points = [new(30, 40), new(38, 40)] });
        using var image = ImageRenderer.RenderBitmap(document);
        Assert.Equal(SKColors.White, image.GetPixel(18, 40));
        Assert.True(image.GetPixel(35, 40).Blue > 200 && image.GetPixel(35, 40).Red < 100);
    }
}
