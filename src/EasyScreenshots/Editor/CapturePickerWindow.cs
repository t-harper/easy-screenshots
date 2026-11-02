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
    private readonly Border _contentHost = new();
    private readonly Border _confirmation;
    private readonly Button _clearButton;
    private readonly TextBlock _count;
    private readonly Action? _clearCaptures;
    private bool _hasCaptures;
    private bool _clearing;
    private readonly TextBlock _status = new()
    {
        Text = "Select a capture to continue editing.  ·  Esc to close",
        Foreground = Brush("#A3AEC1"), FontSize = 12, TextWrapping = TextWrapping.Wrap
    };

    public CapturePickerWindow(IReadOnlyList<CapturePickerItem> items, Action? clearCaptures = null)
    {
        Icon = AppIcon.Default;
        ArgumentNullException.ThrowIfNull(items);
        _clearCaptures = clearCaptures;
        _hasCaptures = items.Count > 0;
        Title = "Easy Screenshots · Captures";
        Width = 1030; Height = 750; MinWidth = 550; MinHeight = 360;
        Background = Brush("#11151D");
        RequestedThemeVariant = ThemeVariant.Dark;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(26, 22, 26, 18) };
        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(new TextBlock { Text = "Your captures", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        _count = new TextBlock { Text = items.Count == 1 ? "1 capture in this session" : $"{items.Count} captures in this session", FontSize = 13, Foreground = Brush("#94A3B8") };
        heading.Children.Add(_count);
        header.Children.Add(heading);
        var close = new Button { Content = "Close", VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(5), Padding = new Thickness(14, 9) };
        close.Click += (_, _) => Close();
        _clearButton = new Button { Content = "Clear captures", VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(5), Padding = new Thickness(14, 9), IsEnabled = _hasCaptures && clearCaptures is not null };
        _clearButton.Click += (_, _) => ShowConfirmation();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_clearButton); actions.Children.Add(close);
        Grid.SetColumn(actions, 1); header.Children.Add(actions); root.Children.Add(header);

        var confirmationPanel = new StackPanel { Spacing = 12 };
        confirmationPanel.Children.Add(new TextBlock { Text = "Discard all captures and unsaved edits? Saved PNGs and drag exports will not be deleted.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 14 });
        var confirmationActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var discard = new Button { Content = "Discard captures", Background = Brush("#9E3442"), Foreground = Brushes.White };
        discard.Click += (_, _) => ClearCaptures();
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => HideConfirmation();
        confirmationActions.Children.Add(discard); confirmationActions.Children.Add(cancel);
        confirmationPanel.Children.Add(confirmationActions);
        _confirmation = new Border { IsVisible = false, Background = Brush("#38242D"), BorderBrush = Brush("#7A3D4A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Margin = new Thickness(24, 0, 24, 16), Padding = new Thickness(16), Child = confirmationPanel };
        Grid.SetRow(_confirmation, 1); root.Children.Add(_confirmation);

        Control content;
        if (items.Count == 0)
        {
            content = EmptyState();
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
        _contentHost.Child = content;
        Grid.SetRow(_contentHost, 2); root.Children.Add(_contentHost);
        var footer = new Border { Background = Brush("#191F2B"), Padding = new Thickness(26, 14), Child = _status };
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            if (_confirmation.IsVisible) HideConfirmation(); else Close();
        };
        Closed += (_, _) => DisposeThumbnails();
    }

    private static Control EmptyState()
    {
        var empty = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(30) };
        empty.Children.Add(new TextBlock { Text = "No captures yet", FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
        empty.Children.Add(new TextBlock { Text = "Take a screenshot to see it here.", FontSize = 14, Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center });
        return empty;
    }

    private void ShowConfirmation()
    {
        if (!_hasCaptures || _clearCaptures is null || _clearing) return;
        _confirmation.IsVisible = true;
        _contentHost.IsEnabled = false;
        _clearButton.IsEnabled = false;
    }

    private void HideConfirmation()
    {
        if (_clearing) return;
        _confirmation.IsVisible = false;
        _contentHost.IsEnabled = true;
        _clearButton.IsEnabled = _hasCaptures && _clearCaptures is not null;
    }

    private void ClearCaptures()
    {
        if (!_confirmation.IsVisible || !_hasCaptures || _clearCaptures is null || _clearing) return;
        _clearing = true;
        try
        {
            _clearCaptures();
            _hasCaptures = false;
            // Removing the entire card tree releases the retained reopen delegates as well as images.
            _contentHost.Child = EmptyState();
            DisposeThumbnails();
            _count.Text = "0 captures in this session";
            _status.Foreground = Brush("#A3AEC1");
            _status.Text = "Captures cleared. Saved PNGs and drag exports were kept.";
            _confirmation.IsVisible = false;
            _contentHost.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _status.Foreground = Brush("#FF9AA2");
            _status.Text = "Could not clear captures: " + ex.Message;
        }
        finally { _clearing = false; }
    }

    private void DisposeThumbnails()
    {
        foreach (var thumbnail in _thumbnails) thumbnail.Dispose();
        _thumbnails.Clear();
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
