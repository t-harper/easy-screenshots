using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace EasyScreenshots.Editor;

public sealed record CapturePickerItem(string Id, string Title, DateTimeOffset CapturedAt,
    int Width, int Height, byte[] ThumbnailPng, Action Reopen);

/// <summary>Selects one retained capture without reopening the other editor windows.</summary>
public sealed class CapturePickerWindow : Window
{
    private readonly List<Bitmap> _thumbnails = [];
    private readonly TextBlock _status = new()
    {
        Text = "Select a capture to continue editing.  ·  Esc to close",
        Foreground = Brush("#A3AEC1"), FontSize = 12, TextWrapping = TextWrapping.Wrap
    };

    public CapturePickerWindow(IReadOnlyList<CapturePickerItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Title = "Easy Screenshots · Captures";
        Width = 1030; Height = 750; MinWidth = 390; MinHeight = 360;
        Background = Brush("#11151D");
        RequestedThemeVariant = ThemeVariant.Dark;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(26, 22, 26, 18) };
        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(new TextBlock { Text = "Your captures", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        heading.Children.Add(new TextBlock { Text = items.Count == 1 ? "1 capture in this session" : $"{items.Count} captures in this session", FontSize = 13, Foreground = Brush("#94A3B8") });
        header.Children.Add(heading);
        var close = new Button { Content = "Close", VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(5), Padding = new Thickness(14, 9) };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);

        Control content;
        if (items.Count == 0)
        {
            var empty = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(30) };
            empty.Children.Add(new TextBlock { Text = "No captures yet", FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            empty.Children.Add(new TextBlock { Text = "Take a screenshot to see it here.", FontSize = 14, Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center });
            content = empty;
        }
        else
        {
            var cards = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 0, 18, 18) };
            foreach (var item in items.OrderByDescending(item => item.CapturedAt)) cards.Children.Add(CreateCard(item));
            content = new ScrollViewer
            {
                Content = cards,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
        }
        Grid.SetRow(content, 1); root.Children.Add(content);
        var footer = new Border { Background = Brush("#191F2B"), Padding = new Thickness(26, 14), Child = _status };
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Closed += (_, _) =>
        {
            foreach (var thumbnail in _thumbnails) thumbnail.Dispose();
            _thumbnails.Clear();
        };
    }

    private Button CreateCard(CapturePickerItem item)
    {
        Control preview;
        try
        {
            using var stream = new MemoryStream(item.ThumbnailPng, writable: false);
            var bitmap = new Bitmap(stream);
            _thumbnails.Add(bitmap);
            preview = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 280, MaxHeight = 170, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        catch (Exception)
        {
            // A damaged thumbnail must not prevent its retained editor from being reopened.
            preview = new TextBlock { Text = "Preview unavailable", Foreground = Brush("#94A3B8"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(new Border { Height = 170, Width = 280, Background = Brush("#0C1017"), CornerRadius = new CornerRadius(5), ClipToBounds = true, Child = preview });
        stack.Children.Add(new TextBlock { Text = item.Title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = item.CapturedAt.ToLocalTime().ToString("g"), Foreground = Brush("#A3AEC1"), FontSize = 12 });
        stack.Children.Add(new TextBlock { Text = $"{item.Width} × {item.Height} px", Foreground = Brush("#7D91AE"), FontSize = 12 });
        var button = new Button
        {
            Tag = item.Id, Content = stack, Width = 304, Margin = new Thickness(6), Padding = new Thickness(11),
            Background = Brush("#1B2432"), BorderBrush = Brush("#303D52"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(button, $"Reopen {item.Title}");
        button.Click += (_, _) =>
        {
            try { item.Reopen(); Close(); }
            catch (Exception ex) { _status.Foreground = Brush("#FF9AA2"); _status.Text = "Could not reopen capture: " + ex.Message; }
        };
        return button;
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
