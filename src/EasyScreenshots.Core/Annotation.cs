namespace EasyScreenshots.Core;

public enum AnnotationKind { Arrow, Text, Rectangle, Ellipse, Pen, Highlighter, Blur }

public sealed record Annotation
{
    private PointD[] _points = [];
    public Guid Id { get; init; } = Guid.NewGuid();
    public AnnotationKind Kind { get; init; }
    // Point buffers are copied at both boundaries so history cannot be modified by callers.
    public PointD[] Points { get => (PointD[])_points.Clone(); init => _points = value is null ? [] : (PointD[])value.Clone(); }
    public uint Color { get; init; } = 0xffff5151;
    public double Thickness { get; init; } = 3;
    public string Text { get; init; } = "Text";
    public double FontSize { get; init; } = 24;
    public RectD Bounds
    {
        get
        {
            if (_points.Length == 0) return default;
            var left = _points.Min(p => p.X); var top = _points.Min(p => p.Y);
            var right = _points.Max(p => p.X); var bottom = _points.Max(p => p.Y);
            if (Kind == AnnotationKind.Text && _points.Length == 1)
            {
                var lines = (Text ?? "").Replace("\r", "").Split('\n');
                return new(left, top, Math.Max(FontSize, lines.Max(line => line.Length) * FontSize * .65), FontSize * (1.4 + (lines.Length - 1) * 1.3));
            }
            return new(left, top, right - left, bottom - top);
        }
    }
    public Annotation Translate(double dx, double dy) => this with { Points = _points.Select(p => new PointD(p.X + dx, p.Y + dy)).ToArray() };
    public Annotation Resize(RectD target)
    {
        var bounds = Bounds;
        var sx = bounds.Width > 0 ? target.Width / bounds.Width : 1;
        var sy = bounds.Height > 0 ? target.Height / bounds.Height : 1;
        return this with
        {
            Points = _points.Select(p => new PointD(target.X + (p.X - bounds.X) * sx, target.Y + (p.Y - bounds.Y) * sy)).ToArray(),
            FontSize = Kind == AnnotationKind.Text ? Math.Max(1, FontSize * sy) : FontSize
        };
    }
}
