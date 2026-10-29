using SkiaSharp;

namespace EasyScreenshots.Core;

public sealed class EditorDocument : IDisposable
{
    private readonly byte[] _originalPng;
    private SKBitmap? _originalBitmap;
    private List<Annotation> _annotations = [];
    private readonly Stack<Annotation[]> _undo = new();
    private readonly Stack<Annotation[]> _redo = new();
    public EditorDocument(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        using var data = SKData.CreateCopy(png);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw new ArgumentException("The image could not be decoded.", nameof(png));
        var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) throw new ArgumentException("The image could not be decoded.", nameof(png));
        _originalBitmap = bitmap;
        Width = bitmap.Width; Height = bitmap.Height; _originalPng = (byte[])png.Clone();
    }
    public int Width { get; }
    public int Height { get; }
    public byte[] OriginalPng => (byte[])_originalPng.Clone();
    internal SKBitmap OriginalBitmap => _originalBitmap ?? throw new ObjectDisposedException(nameof(EditorDocument));
    public IReadOnlyList<Annotation> Annotations => _annotations.AsReadOnly();
    public bool CanUndo => _undo.Count != 0;
    public bool CanRedo => _redo.Count != 0;
    public event EventHandler? Changed;
    private void SaveUndo() { _undo.Push(_annotations.ToArray()); _redo.Clear(); }
    public void Add(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (_annotations.Any(a => a.Id == annotation.Id)) throw new ArgumentException("Annotation ID already exists.", nameof(annotation));
        SaveUndo(); _annotations.Add(annotation); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Replace(Annotation annotation)
    {
        var index = _annotations.FindIndex(a => a.Id == annotation.Id);
        if (index < 0) return;
        SaveUndo(); _annotations[index] = annotation; Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Remove(Guid id)
    {
        if (!_annotations.Any(a => a.Id == id)) return;
        SaveUndo(); _annotations.RemoveAll(a => a.Id == id); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Undo()
    {
        if (!_undo.TryPop(out var previous)) return;
        _redo.Push(_annotations.ToArray()); _annotations = previous.ToList(); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Redo()
    {
        if (!_redo.TryPop(out var next)) return;
        _undo.Push(_annotations.ToArray()); _annotations = next.ToList(); Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _originalBitmap?.Dispose();
        _originalBitmap = null;
    }
}
