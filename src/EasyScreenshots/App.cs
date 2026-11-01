using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using EasyScreenshots.Capture;
using EasyScreenshots.Core;
using EasyScreenshots.Editor;
using EasyScreenshots.Platform;
using Microsoft.Win32;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace EasyScreenshots;

public sealed class App : Application
{
    private IClassicDesktopStyleApplicationLifetime _desktop = null!;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyScreenshots", "settings.json");
    private AppSettings _settings = new();
    private GlobalHotkey? _hotkey;
    private TrayIcon? _tray;
    private Window? _home;
    private Window? _options;
    private Window? _capturePicker;
    private int _captureNumber;
    private readonly List<Window> _editors = [];
    private bool _capturing;
    private bool _exiting;
    private string _hotkeyStatus = "Ctrl+Shift+4 or Win+Shift+4 — capture anywhere";

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>())
        {
            Setters = { new Setter(Button.PaddingProperty, new Thickness(14, 9)), new Setter(Button.CornerRadiusProperty, new CornerRadius(6)) }
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _settings = SettingsStore.Load(_settingsPath);
            try { _hotkey = new GlobalHotkey(() => _ = CaptureAsync()); }
            catch (Exception ex) { _hotkeyStatus = "Shortcut unavailable: " + ex.Message; }
            SetupTray();
            desktop.Exit += (_, _) => { _hotkey?.Dispose(); _tray?.Dispose(); };
            if (desktop.Args?.Contains("--background") != true || _hotkey is null) ShowHome();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray()
    {
        using var bitmap = new SKBitmap(32, 32);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(20, 24, 35));
            using var paint = new SKPaint { Color = new SKColor(100, 175, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(5, 8, 27, 25), 3, 3, paint);
            canvas.DrawCircle(16, 16, 5, paint);
        }
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(encoded.ToArray());
        var menu = new NativeMenu();
        var capture = new NativeMenuItem("New capture  ·  Ctrl+Shift+4 / Win+Shift+4");
        capture.Click += (_, _) => _ = CaptureAsync();
        var open = new NativeMenuItem("Open Easy Screenshots");
        open.Click += (_, _) => ShowHome();
        var options = new NativeMenuItem("Options");
        options.Click += (_, _) => ShowOptions();
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => Exit();
        var last = new NativeMenuItem("Reopen captures…");
        last.Click += (_, _) => ShowCapturePicker();
        menu.Items.Add(capture); menu.Items.Add(open); menu.Items.Add(last); menu.Items.Add(options); menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exit);
        _tray = new TrayIcon { Icon = new WindowIcon(stream), ToolTipText = "Easy Screenshots · Ctrl+Shift+4 / Win+Shift+4", Menu = menu, IsVisible = true };
        _tray.Clicked += (_, _) => ShowHome();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void ShowHome()
    {
        if (_home is not null) { _home.Show(); _home.Activate(); return; }
        var stack = new StackPanel { Spacing = 18, Margin = new Thickness(32) };
        stack.Children.Add(new TextBlock { Text = "Easy Screenshots", FontSize = 30, FontWeight = FontWeight.SemiBold });
        stack.Children.Add(new TextBlock { Text = "Capture a moment. Make your point.", FontSize = 16, Foreground = Brushes.LightGray });
        stack.Children.Add(new TextBlock { Text = _hotkeyStatus, TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "＋  New capture", HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => _ = CaptureAsync(); stack.Children.Add(button);
        var options = new Button { Content = "Options" }; options.Click += (_, _) => ShowOptions(); stack.Children.Add(options);
        var captures = new Button { Content = "Reopen captures" };
        captures.Click += (_, _) => ShowCapturePicker();
        stack.Children.Add(captures);
        stack.Children.Add(new TextBlock { Text = "Drag to select · Escape to cancel\nEdit with arrows, text, shapes, pen and blur.\nCtrl+S to save · Drag image to share a PNG.", Foreground = Brushes.Gray, LineHeight = 24 });
        _home = new Window { Title = "Easy Screenshots", Width = 500, Height = 580, CanResize = false, Content = new ScrollViewer { Content = stack }, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        AttachLifetime(_home); _home.Show();
    }

    private async Task CaptureAsync()
    {
        if (_capturing) return;
        _capturing = true;
        var visible = _editors.Append(_home).Append(_options).Append(_capturePicker).Where(w => w?.IsVisible == true).Cast<Window>().ToArray();
        foreach (var window in visible) window.Hide();
        try
        {
            // Wait for the compositor only if we actually hid a window. The tray
            // shortcut needs no artificial delay before freezing the desktop.
            if (visible.Length > 0) DwmFlush();
            var png = await new CaptureCoordinator().CaptureAsync();
            foreach (var window in visible) window.Show();
            if (png is not null)
            {
                var editor = new EditorWindow(png, () => _ = CaptureAsync(), ShowOptions);
                editor.CaptureName = $"Capture {++_captureNumber}";
                editor.Title = $"Easy Screenshots · {editor.CaptureName}";
                _editors.Add(editor); editor.Closed += (_, _) => _editors.Remove(editor);
                AttachLifetime(editor); editor.Show(); editor.Activate();
                _capturePicker?.Close();
            }
        }
        catch (Exception ex)
        {
            foreach (var window in visible) window.Show();
            ShowMessage("Capture failed", ex.Message);
        }
        finally { _capturing = false; }
    }

    private void AttachLifetime(Window window)
    {
        window.Closing += (_, e) =>
        {
            if (_exiting) return;
            if (_settings.CloseToTray) { e.Cancel = true; window.Hide(); }
            else if (_desktop.Windows.Count(w => w.IsVisible && w != window) == 0) Exit();
        };
        window.Closed += (_, _) => { if (window == _home) _home = null; };
    }

    private void ShowCapturePicker()
    {
        if (_capturePicker is not null) { _capturePicker.Show(); _capturePicker.Activate(); return; }
        try
        {
            var items = _editors.OfType<EditorWindow>().Select(editor => new CapturePickerItem(
                editor.CaptureName, editor.CaptureName, editor.CapturedAt,
                editor.CaptureWidth, editor.CaptureHeight, editor.GetThumbnailPng(), () =>
                {
                    if (!_editors.Contains(editor)) throw new InvalidOperationException("This capture is no longer open.");
                    editor.Show(); editor.Activate();
                })).ToArray();
            var picker = new CapturePickerWindow(items);
            _capturePicker = picker;
            picker.Closed += (_, _) =>
            {
                _capturePicker = null;
                if (!_exiting && !_settings.CloseToTray && !_desktop.Windows.Any(w => w.IsVisible)) ShowHome();
            };
            picker.Show(); picker.Activate();
        }
        catch (Exception ex) { ShowMessage("Could not open captures", ex.Message); }
    }

    private void ShowOptions()
    {
        if (_options is not null) { _options.Show(); _options.Activate(); return; }
        var tray = new CheckBox { Content = "Keep running in tray when windows close", IsChecked = _settings.CloseToTray };
        var startup = new CheckBox { Content = "Start at Windows sign-in", IsChecked = _settings.StartAtLogin };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
        var save = new Button { Content = "Save options" };
        var panel = new StackPanel { Margin = new Thickness(26), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = "Options", FontSize = 24, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(tray); panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "Global capture: Ctrl+Shift+4 or Win+Shift+4\nWin+Shift+4 replaces the taskbar shortcut while this app runs.\nKeep the portable app in a permanent location if you enable sign-in startup.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray });
        panel.Children.Add(save); panel.Children.Add(status);
        save.Click += (_, _) =>
        {
            try
            {
                var next = new AppSettings(tray.IsChecked == true, startup.IsChecked == true);
                SetStartup(next.StartAtLogin);
                SettingsStore.Save(_settingsPath, next); _settings = next;
                status.Text = "Options saved.";
            }
            catch (Exception ex) { status.Text = "Could not save options: " + ex.Message; }
        };
        _options = new Window { Title = "Easy Screenshots · Options", Width = 490, Height = 430, Content = new ScrollViewer { Content = panel }, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        _options.Closed += (_, _) => _options = null;
        AttachLifetime(_options); _options.Show();
    }

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find executable path.");
            if (Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Enable startup from the published EasyScreenshots.exe.");
            key.SetValue("EasyScreenshots", $"\"{path}\" --background");
        }
        else key.DeleteValue("EasyScreenshots", false);
    }

    private static void ShowMessage(string title, string message)
    {
        var window = new Window { Title = title, Width = 520, Height = 240, Content = new TextBlock { Text = message, Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap } };
        window.Show();
    }

    private void Exit() { _exiting = true; _desktop.Shutdown(); }

    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
