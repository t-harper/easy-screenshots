using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EasyScreenshots.Core;
using System.Runtime.InteropServices;

namespace EasyScreenshots.Editor;

/// <summary>All interaction is expressed in original screenshot pixels; zoom never changes annotations.</summary>
public sealed class AnnotationSurface : Control, IDisposable
{
    private readonly EditorDocument _document;
    private Bitmap? _preview;
    private Annotation? _draft;
    private Annotation? _before;
    private PointD _start;
    private readonly List<PointD> _stroke = [];
    private bool _moving, _resizing;
    private double _zoom = 1;
    public AnnotationKind? Tool { get; set; }
    public uint Ink { get; set; } = 0xFFFF5964;
    public double Thickness { get; set; } = 4;
    public double FontSize { get; set; } = 28;
    public string Text { get; set; } = "Text";
    public Guid? SelectedId { get; private set; }
    public event Action<Annotation?>? SelectionChanged;
    public event Action<string>? Error;

    public AnnotationSurface(EditorDocument document)
    {
        _document = document;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        _document.Changed += DocumentChanged;
        Refresh();
    }

    public double Zoom
    {
        get => _zoom;
        set { _zoom = Math.Clamp(value, .1, 4); Width = _document.Width * _zoom; Height = _document.Height * _zoom; InvalidateVisual(); }
    }

    public Annotation? Selected => _document.Annotations.FirstOrDefault(a => a.Id == SelectedId);

    private void DocumentChanged(object? sender, EventArgs e)
    {
        if (SelectedId is not null && Selected is null) Select(null);
        else if (SelectedId is not null) SelectionChanged?.Invoke(Selected);
        Refresh();
    }

    public void Select(Annotation? annotation)
    {
        SelectedId = annotation?.Id;
        SelectionChanged?.Invoke(annotation);
        InvalidateVisual();
    }

    public void UpdateSelected(Func<Annotation, Annotation> edit)
    {
        if (Selected is { } item) _document.Replace(edit(item));
    }

    private void Refresh()
    {
        try
        {
            using var pixels = ImageRenderer.RenderBitmap(_document, _draft);
            var next = new WriteableBitmap(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var framebuffer = next.Lock())
            {
                var row = new byte[pixels.Width * 4];
                for (var y = 0; y < pixels.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(pixels.GetPixels(), y * pixels.RowBytes), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), row.Length);
                }
            }
            _preview?.Dispose();
            _preview = next;
            Width = _document.Width * _zoom;
            Height = _document.Height * _zoom;
            InvalidateVisual();
        }
        catch (Exception ex) { Error?.Invoke("Preview failed: " + ex.Message); }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_preview is not null)
            context.DrawImage(_preview, new Rect(0, 0, _document.Width, _document.Height), new Rect(0, 0, Width, Height));
        var selected = _draft ?? Selected;
        if (selected is null) return;
        var b = selected.Bounds;
        var rect = new Rect(b.X * _zoom, b.Y * _zoom, Math.Max(1, b.Width * _zoom), Math.Max(1, b.Height * _zoom));
        context.DrawRectangle(null, new Pen(Brushes.White, 1), rect.Inflate(2));
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#76B7FF")), new Pen(Brushes.White, 1), new Rect(rect.Right - 5, rect.Bottom - 5, 10, 10));
    }

    private PointD ImagePoint(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        return new PointD(Math.Clamp(p.X / _zoom, 0, _document.Width), Math.Clamp(p.Y / _zoom, 0, _document.Height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        _start = ImagePoint(e);
        _before = null; _draft = null; _moving = _resizing = false;
        if (Tool is null)
        {
            var selected = Selected;
            if (selected is not null && Math.Abs(_start.X - selected.Bounds.X - selected.Bounds.Width) * _zoom <= 9 && Math.Abs(_start.Y - selected.Bounds.Y - selected.Bounds.Height) * _zoom <= 9)
                { _before = selected; _resizing = true; }
            else
            {
                var hit = _document.Annotations.LastOrDefault(a => Hit(a, _start));
                Select(hit);
                _before = hit;
                _moving = hit is not null;
            }
        }
        else
        {
            Select(null);
            _stroke.Clear(); _stroke.Add(_start);
            _draft = new Annotation { Id = Guid.NewGuid(), Kind = Tool.Value, Points = Tool == AnnotationKind.Text ? [_start] : [_start, _start], Color = Ink, Thickness = Thickness, Text = string.IsNullOrWhiteSpace(Text) ? "Text" : Text, FontSize = FontSize };
            Refresh();
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private bool Hit(Annotation annotation, PointD p)
    {
        var b = annotation.Bounds;
        var pad = Math.Max(5 / _zoom, annotation.Thickness / 2);
        return p.X >= b.X - pad && p.X <= b.X + b.Width + pad && p.Y >= b.Y - pad && p.Y <= b.Y + b.Height + pad;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured != this) return;
        var point = ImagePoint(e);
        if (!_moving && !_resizing && _draft?.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            var dx = point.X - _start.X; var dy = point.Y - _start.Y;
            var sx = dx < 0 ? -1 : 1; var sy = dy < 0 ? -1 : 1;
            var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
            size = Math.Min(size, Math.Min(sx > 0 ? _document.Width - _start.X : _start.X, sy > 0 ? _document.Height - _start.Y : _start.Y));
            point = new PointD(_start.X + sx * size, _start.Y + sy * size);
        }
        if (_moving && _before is not null) _draft = _before.Translate(point.X - _start.X, point.Y - _start.Y);
        else if (_resizing && _before is not null)
        {
            var b = _before.Bounds;
            _draft = _before.Resize(new RectD(b.X, b.Y, Math.Max(1, point.X - b.X), Math.Max(1, point.Y - b.Y)));
        }
        else if (_draft is not null)
        {
            if (_draft.Kind is AnnotationKind.Pen or AnnotationKind.Highlighter)
            {
                if (_stroke.Count == 0 || Math.Abs(point.X - _stroke[^1].X) + Math.Abs(point.Y - _stroke[^1].Y) >= .6)
                    _stroke.Add(point);
                _draft = _draft with { Points = _stroke.ToArray() };
            }
            else if (_draft.Kind != AnnotationKind.Text) _draft = _draft with { Points = [_start, point] };
        }
        Refresh();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured != this) return;
        var draft = _draft;
        _draft = null;
        e.Pointer.Capture(null);
        if (draft is not null)
        {
            if (_before is not null) _document.Replace(draft);
            else if (draft.Kind == AnnotationKind.Text || draft.Bounds.Width > 1 || draft.Bounds.Height > 1) _document.Add(draft);
            Select(_document.Annotations.FirstOrDefault(a => a.Id == draft.Id));
        }
        _before = null;
        _moving = _resizing = false;
        Refresh();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_draft is null) return;
        _draft = null; _before = null; _moving = _resizing = false;
        Refresh();
    }

    public void CancelGesture()
    {
        _draft = null; _before = null; _moving = _resizing = false;
        Refresh();
    }

    public void Dispose()
    {
        _document.Changed -= DocumentChanged;
        _preview?.Dispose();
    }
}
