using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EasyScreenshots.Core;

namespace EasyScreenshots.Editor;

public sealed class EditorWindow : Window
{
    private readonly EditorDocument _document;
    private readonly AnnotationSurface _surface;
    private readonly TextBlock _status = new() { Foreground = new SolidColorBrush(Color.Parse("#A3AEC1")), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _text = new() { Text = "Text", Watermark = "Annotation text", AcceptsReturn = true, MinHeight = 65, TextWrapping = TextWrapping.Wrap };
    private readonly Slider _thickness = new() { Minimum = 1, Maximum = 32, Value = 4, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly Slider _font = new() { Minimum = 10, Maximum = 96, Value = 28, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly List<(AnnotationKind? Kind, Button Button)> _toolButtons = [];
    private readonly Button _undo;
    private readonly Button _redo;
    private bool _syncing;
    private byte[]? _thumbnailPng;
    public DateTimeOffset CapturedAt { get; } = DateTimeOffset.Now;
    public string CaptureName { get; set; } = "Capture";
    public int CaptureWidth => _document.Width;
    public int CaptureHeight => _document.Height;

    public byte[] GetThumbnailPng() => _thumbnailPng ??= ImageRenderer.RenderThumbnailPng(_document);

    public EditorWindow(byte[] png, Action requestCapture, Action requestOptions)
    {
        Icon = AppIcon.Default;
        _document = new EditorDocument(png);
        _surface = new AnnotationSurface(_document);
        Title = "Easy Screenshots";
        Width = 1200; Height = 820; MinWidth = 820; MinHeight = 600;
        Background = Brush("#11151D");
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        _undo = ActionButton("↶ Undo", () => _document.Undo());
        _redo = ActionButton("↷ Redo", () => _document.Redo());

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(20, 14) };
        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock { Text = "Easy Screenshots", FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        title.Children.Add(new TextBlock { Text = $"{_document.Width} × {_document.Height} px  ·  PNG", FontSize = 12, Foreground = Brush("#94A3B8") });
        top.Children.Add(title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(ActionButton("New capture", requestCapture));
        actions.Children.Add(ActionButton("Options", requestOptions));
        actions.Children.Add(ActionButton("Save PNG", () => _ = SaveAsync()));
        var drag = new Border { Background = Brush("#2962C7"), CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 10), Cursor = new Cursor(StandardCursorType.Hand), Child = new TextBlock { Text = "⤴ Drag image", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } };
        ToolTip.SetTip(drag, "Drag the annotated PNG into a folder or an app that accepts files.");
        drag.PointerPressed += StartDrag;
        actions.Children.Add(drag);
        Grid.SetColumn(actions, 1); top.Children.Add(actions);
        root.Children.Add(top);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("208,*") };
        var tools = new StackPanel { Spacing = 6, Margin = new Thickness(14, 12) };
        tools.Children.Add(Label("ANNOTATE"));
        AddTool(tools, null, "↖  Select / move");
        AddTool(tools, AnnotationKind.Arrow, "↗  Arrow");
        AddTool(tools, AnnotationKind.Text, "T   Text");
        AddTool(tools, AnnotationKind.Pen, "✎  Pen");
        AddTool(tools, AnnotationKind.Highlighter, "▰  Highlighter");
        AddTool(tools, AnnotationKind.Rectangle, "□  Rectangle");
        AddTool(tools, AnnotationKind.Ellipse, "○  Ellipse");
        AddTool(tools, AnnotationKind.Blur, "▧  Blur");
        tools.Children.Add(Label("COLOR"));
        var palette = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var hex in new[] { "#FF5964", "#FFBE55", "#F8ED62", "#55D6A0", "#62A8FF", "#BA8CFF", "#FFFFFF", "#111111" })
        {
            var color = Color.Parse(hex);
            var swatch = new Button { Width = 33, Height = 27, Margin = new Thickness(2), Background = new SolidColorBrush(color), BorderBrush = Brush("#667085"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            ToolTip.SetTip(swatch, hex);
            swatch.Click += (_, _) => { _surface.Ink = color.ToUInt32(); if (!_syncing) _surface.UpdateSelected(a => a with { Color = color.ToUInt32() }); };
            palette.Children.Add(swatch);
        }
        tools.Children.Add(palette);
        tools.Children.Add(Label("THICKNESS")); tools.Children.Add(_thickness);
        tools.Children.Add(Label("TEXT / FONT SIZE")); tools.Children.Add(_text); tools.Children.Add(_font);
        tools.Children.Add(ActionButton("Delete selected", DeleteSelected));
        tools.Children.Add(new TextBlock { Text = "Press V to select and move.\nDrag the blue corner to resize.\nShift: draw a square or circle.\nText remains editable here.", FontSize = 11, Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6) });
        var toolScroll = new ScrollViewer { Content = tools, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        body.Children.Add(new Border { Background = Brush("#191F2B"), Child = toolScroll });
        var canvasBackground = new Border { Background = Brush("#0C1017"), Padding = new Thickness(30), Child = _surface, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var viewport = new ScrollViewer { Content = canvasBackground, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Grid.SetColumn(viewport, 1); body.Children.Add(viewport); Grid.SetRow(body, 1); root.Children.Add(body);

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(14, 9) };
        bottom.Children.Add(_undo); Grid.SetColumn(_redo, 1); bottom.Children.Add(_redo);
        _status.Text = "Ready  ·  Ctrl+S save  ·  Ctrl+Z undo"; _status.Margin = new Thickness(16, 0); Grid.SetColumn(_status, 2); bottom.Children.Add(_status);
        var zoom = new ComboBox { Width = 100, ItemsSource = new[] { "25%", "50%", "75%", "100%", "150%", "200%" }, SelectedIndex = 3 };
        zoom.SelectionChanged += (_, _) => { if (zoom.SelectedItem is string s) _surface.Zoom = double.Parse(s.TrimEnd('%')) / 100; };
        Grid.SetColumn(zoom, 3); bottom.Children.Add(zoom); Grid.SetRow(bottom, 2); root.Children.Add(bottom);
        Content = root;
        _surface.SelectionChanged += SyncSelection;
        _surface.Error += message => _status.Text = message;
        _document.Changed += (_, _) => { _thumbnailPng = null; _undo.IsEnabled = _document.CanUndo; _redo.IsEnabled = _document.CanRedo; };
        _undo.IsEnabled = _redo.IsEnabled = false;
        _thickness.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { _surface.Thickness = _thickness.Value; if (!_syncing) _surface.UpdateSelected(a => a with { Thickness = _thickness.Value }); } };
        _font.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { _surface.FontSize = _font.Value; if (!_syncing) _surface.UpdateSelected(a => a with { FontSize = _font.Value }); } };
        _text.TextChanged += (_, _) =>
        {
            _surface.Text = _text.Text ?? "";
            // TextChanged can be dispatched after SyncSelection returns; ignore sidebar synchronization.
            if (!_syncing && _surface.Selected is { Kind: AnnotationKind.Text } selected && selected.Text != _surface.Text)
                _surface.UpdateSelected(a => a with { Text = _surface.Text });
        };
        KeyDown += HandleKey;
        Closed += (_, _) => { _surface.Dispose(); _document.Dispose(); };
        SetTool(null);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush("#8A9AB4"), Margin = new Thickness(3, 10, 0, 3), LetterSpacing = 1.2 };
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(5) };
        button.Click += (_, _) => action();
        return button;
    }
    private void AddTool(StackPanel panel, AnnotationKind? kind, string text)
    {
        var button = ActionButton(text, () => SetTool(kind));
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        _toolButtons.Add((kind, button)); panel.Children.Add(button);
    }
    private void SetTool(AnnotationKind? kind)
    {
        _surface.Tool = kind;
        _surface.Cursor = new Cursor(kind is null ? StandardCursorType.Arrow : StandardCursorType.Cross);
        foreach (var entry in _toolButtons)
            entry.Button.Background = Brush(entry.Kind == kind ? "#284C7D" : "#222B3A");
        _status.Text = kind is null ? "Select · drag to move · blue corner to resize" : $"{kind} · drag on the screenshot";
    }
    private void SyncSelection(Annotation? annotation)
    {
        if (annotation is null) return;
        _syncing = true;
        _thickness.Value = annotation.Thickness; _font.Value = annotation.FontSize;
        _text.Text = annotation.Text;
        _surface.Ink = annotation.Color;
        _syncing = false;
    }
    private void DeleteSelected() { if (_surface.SelectedId is { } id) _document.Remove(id); }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S) { _ = SaveAsync(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z && !_text.IsKeyboardFocusWithin)
        { if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _document.Redo(); else _document.Undo(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y && !_text.IsKeyboardFocusWithin) { _document.Redo(); e.Handled = true; }
        else if (e.Key == Key.Delete && !_text.IsKeyboardFocusWithin) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _surface.CancelGesture(); _surface.Select(null); SetTool(null); e.Handled = true; }
        else if (e.Key == Key.V && e.KeyModifiers == KeyModifiers.None && !_text.IsKeyboardFocusWithin) SetTool(null);
    }

    private async Task SaveAsync()
    {
        try
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save screenshot", SuggestedFileName = $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png", DefaultExtension = "png", ShowOverwritePrompt = true, FileTypeChoices = [new FilePickerFileType("PNG image") { Patterns = ["*.png"] }] });
            if (target is null) return;
            var bytes = ImageRenderer.RenderPng(_document);
            await using var stream = await target.OpenWriteAsync();
            stream.SetLength(0);
            await stream.WriteAsync(bytes);
            _status.Text = "Saved " + target.Name;
        }
        catch (Exception ex) { _status.Text = "Save failed: " + ex.Message; }
    }

    private async void StartDrag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyScreenshots", "DragExports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(path, ImageRenderer.RenderPng(_document));
            var file = await StorageProvider.TryGetFileFromPathAsync(path);
            if (file is null) throw new IOException("Could not prepare the PNG for dragging.");
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(file));
            data.Add(DataTransferItem.CreateText(path));
            _status.Text = "Drop the PNG into a folder or an app that accepts files";
            var result = await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
            _status.Text = result == DragDropEffects.None ? "Drag cancelled" : "PNG handed to destination";
        }
        catch (Exception ex) { _status.Text = "Drag failed: " + ex.Message; }
    }
}
