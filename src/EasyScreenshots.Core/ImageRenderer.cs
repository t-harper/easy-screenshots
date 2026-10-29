using SkiaSharp;

namespace EasyScreenshots.Core;

public static class ImageRenderer
{
    public static byte[] RenderPng(EditorDocument document)
    {
        using var bitmap = RenderBitmap(document);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    /// <summary>Returns a caller-owned BGRA premultiplied bitmap. A transient draft never modifies document history.</summary>
    public static SKBitmap RenderBitmap(EditorDocument document, Annotation? draft = null)
    {
        var original = document.OriginalBitmap;
        var bitmap = new SKBitmap(document.Width, document.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(original, 0, 0);
            var replaced = false;
            foreach (var annotation in document.Annotations)
            {
                if (draft is not null && annotation.Id == draft.Id) { Draw(canvas, bitmap, draft); replaced = true; }
                else Draw(canvas, bitmap, annotation);
            }
            if (draft is not null && !replaced) Draw(canvas, bitmap, draft);
            canvas.Flush();
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private static SKRect Rect(RectD r) => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    private static void Draw(SKCanvas canvas, SKBitmap bitmap, Annotation a)
    {
        var points = a.Points;
        if (points.Length == 0) return;
        var thickness = (float)Math.Clamp(a.Thickness, .5, 200);
        using var paint = new SKPaint { Color = new SKColor(a.Color), StrokeWidth = thickness, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        switch (a.Kind)
        {
            case AnnotationKind.Arrow:
                DrawArrow(canvas, points, paint.Color, thickness);
                break;
            case AnnotationKind.Rectangle: canvas.DrawRect(Rect(a.Bounds), paint); break;
            case AnnotationKind.Ellipse: canvas.DrawOval(Rect(a.Bounds), paint); break;
            case AnnotationKind.Text:
                paint.Style = SKPaintStyle.Fill; paint.TextSize = (float)Math.Clamp(a.FontSize, 1, 1000);
                using (var typeface = SKTypeface.FromFamilyName("Segoe UI"))
                {
                    paint.Typeface = typeface;
                    var lines = (a.Text ?? "").Replace("\r", "").Split('\n');
                    for (var i = 0; i < lines.Length; i++) canvas.DrawText(lines[i], (float)points[0].X, (float)points[0].Y + paint.TextSize * (1 + i * 1.3f), paint);
                }
                break;
            case AnnotationKind.Blur:
                var region = a.Bounds.Intersect(new(0, 0, bitmap.Width, bitmap.Height));
                if (region.Width <= 0 || region.Height <= 0) break;
                // Snapshot the current composite: blur includes annotations below this retained layer.
                using (var snapshot = bitmap.Copy())
                using (var filter = SKImageFilter.CreateBlur(Math.Max(3, thickness), Math.Max(3, thickness), SKShaderTileMode.Clamp))
                using (var blur = new SKPaint { ImageFilter = filter })
                {
                    canvas.Save(); canvas.ClipRect(Rect(region)); canvas.DrawBitmap(snapshot, 0, 0, blur); canvas.Restore();
                }
                break;
            default:
                if (a.Kind == AnnotationKind.Highlighter) { paint.Color = paint.Color.WithAlpha(85); paint.StrokeWidth = thickness * 4; paint.StrokeCap = SKStrokeCap.Square; }
                using (var path = new SKPath())
                {
                    path.MoveTo((float)points[0].X, (float)points[0].Y);
                    foreach (var p in points.Skip(1)) path.LineTo((float)p.X, (float)p.Y);
                    if (points.Length == 1) canvas.DrawPoint((float)points[0].X, (float)points[0].Y, paint);
                    else canvas.DrawPath(path, paint);
                }
                break;
        }
    }

    private static void DrawArrow(SKCanvas canvas, PointD[] points, SKColor color, float thickness)
    {
        if (points.Length < 2) return;
        var start = points[0]; var end = points[^1];
        var dx = end.X - start.X; var dy = end.Y - start.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (length < .5f) return;

        // A single silhouette avoids seams where the tapered shaft meets its filled head.
        // Limit every dimension by length so a tiny drag never produces a backwards head.
        var headLength = Math.Min(Math.Max(14, thickness * 4), length * .65f);
        var headHalfWidth = Math.Min(Math.Max(7, thickness * 1.8f), headLength * .55f);
        var tailHalfWidth = Math.Min(thickness * .42f, length * .16f);
        var neckHalfWidth = Math.Min(thickness * .55f, headHalfWidth * .45f);
        var neck = length - headLength;
        using var path = new SKPath();
        path.MoveTo(0, -tailHalfWidth);
        path.LineTo(neck, -neckHalfWidth);
        path.LineTo(neck, -headHalfWidth);
        path.LineTo(length, 0);
        path.LineTo(neck, headHalfWidth);
        path.LineTo(neck, neckHalfWidth);
        path.LineTo(0, tailHalfWidth);
        path.CubicTo(-tailHalfWidth * 1.33f, tailHalfWidth, -tailHalfWidth * 1.33f, -tailHalfWidth, 0, -tailHalfWidth);
        path.Close();

        var luminance = .2126 * color.Red + .7152 * color.Green + .0722 * color.Blue;
        var edgeColor = luminance > 160 ? SKColors.Black : SKColors.White;
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = Math.Min(1.5f, Math.Max(.7f, thickness * .15f)), Color = edgeColor.WithAlpha((byte)(color.Alpha * .45f)) };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = color };
        canvas.Save();
        canvas.Translate((float)start.X, (float)start.Y);
        canvas.RotateDegrees((float)(Math.Atan2(dy, dx) * 180 / Math.PI));
        canvas.DrawPath(path, edge);
        canvas.DrawPath(path, fill);
        canvas.Restore();
    }
}
