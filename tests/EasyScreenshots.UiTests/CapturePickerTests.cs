using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using EasyScreenshots.Editor;
using SkiaSharp;
using Xunit;

namespace EasyScreenshots.UiTests;

public sealed class CapturePickerTests
{
    private static byte[] Thumbnail()
    {
        using var bitmap = new SKBitmap(80, 40);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static Button[] Cards(Window window) => window.GetVisualDescendants().OfType<Button>().Where(b => b.Tag is string).ToArray();
    private static Button ButtonNamed(Window window, string label) => Assert.Single(window.GetVisualDescendants().OfType<Button>().Where(b => Equals(b.Content, label)));
    private static void Click(Window window, Button button)
    {
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Clicking_one_thumbnail_reopens_only_that_capture_and_closes_the_picker()
    {
        var first = 0; var second = 0;
        var now = DateTimeOffset.Now;
        var picker = new CapturePickerWindow([
            new("first", "First capture", now, 1920, 1080, Thumbnail(), () => first++),
            new("second", "Second capture", now.AddMinutes(-1), 800, 600, Thumbnail(), () => second++)]);
        picker.Show();
        try
        {
            Click(picker, Assert.Single(Cards(picker).Where(b => Equals(b.Tag, "second"))));
            Assert.Equal(0, first); Assert.Equal(1, second);
            Assert.False(picker.IsVisible);
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Captures_are_newest_first_with_title_local_time_original_dimensions_and_uncropped_thumbnail()
    {
        var time = new DateTimeOffset(2026, 11, 1, 17, 45, 0, TimeSpan.Zero);
        var picker = new CapturePickerWindow([
            new("old", "Earlier capture", time.AddMinutes(-2), 3840, 2160, Thumbnail(), () => { }),
            new("new", "Latest capture", time, 1024, 768, Thumbnail(), () => { })]);
        picker.Show();
        try
        {
            var cards = Cards(picker);
            Assert.Equal(new[] { "new", "old" }, cards.Select(b => (string)b.Tag!));
            var labels = cards[0].GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains("Latest capture", labels);
            Assert.Contains(time.ToLocalTime().ToString("g"), labels);
            Assert.Contains("1024 × 768 px", labels);
            var image = Assert.Single(cards[0].GetVisualDescendants().OfType<Image>());
            Assert.NotNull(image.Source);
            Assert.Equal(Stretch.Uniform, image.Stretch);
            Assert.True(image.MaxWidth <= 280 && image.MaxHeight <= 170);
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Empty_picker_explains_there_are_no_captures_and_escape_closes_it()
    {
        var picker = new CapturePickerWindow([]);
        picker.Show();
        try
        {
            Assert.Empty(Cards(picker));
            Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "No captures yet");
            picker.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.False(picker.IsVisible);
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Reopen_failure_keeps_picker_open_and_shows_the_error()
    {
        var picker = new CapturePickerWindow([new("broken", "Unavailable", DateTimeOffset.Now, 100, 100, Thumbnail(), () => throw new InvalidOperationException("Capture is unavailable"))]);
        picker.Show();
        try
        {
            Click(picker, Assert.Single(Cards(picker)));
            Assert.True(picker.IsVisible);
            Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Capture is unavailable") == true);
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Clear_requires_confirmation_and_cancel_or_escape_preserves_captures()
    {
        var cleared = 0;
        var picker = new CapturePickerWindow([new("one", "Capture", DateTimeOffset.Now, 100, 100, Thumbnail(), () => { })], () => cleared++);
        picker.Show();
        try
        {
            Click(picker, ButtonNamed(picker, "Clear captures"));
            Assert.Equal(0, cleared);
            Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Discard all captures and unsaved edits? Saved PNGs and drag exports will not be deleted.");
            Click(picker, ButtonNamed(picker, "Cancel"));
            Assert.Equal(0, cleared); Assert.Single(Cards(picker));
            Click(picker, ButtonNamed(picker, "Clear captures"));
            picker.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            picker.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.True(picker.IsVisible);
            Assert.Equal(0, cleared); Assert.Single(Cards(picker));
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Confirm_clear_calls_action_once_removes_thumbnails_and_disables_clear()
    {
        var cleared = 0;
        var picker = new CapturePickerWindow([new("one", "Capture", DateTimeOffset.Now, 100, 100, Thumbnail(), () => { })], () => cleared++);
        picker.Show();
        try
        {
            Click(picker, ButtonNamed(picker, "Clear captures"));
            Click(picker, ButtonNamed(picker, "Discard captures"));
            Assert.Equal(1, cleared); Assert.True(picker.IsVisible);
            Assert.Empty(Cards(picker));
            Assert.Empty(picker.GetVisualDescendants().OfType<Image>());
            Assert.False(ButtonNamed(picker, "Clear captures").IsEnabled);
            Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "No captures yet");
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Clear_failure_shows_error_and_preserves_cards()
    {
        var picker = new CapturePickerWindow([new("one", "Capture", DateTimeOffset.Now, 100, 100, Thumbnail(), () => { })], () => throw new InvalidOperationException("Could not discard"));
        picker.Show();
        try
        {
            Click(picker, ButtonNamed(picker, "Clear captures"));
            Click(picker, ButtonNamed(picker, "Discard captures"));
            Assert.True(picker.IsVisible); Assert.Single(Cards(picker));
            Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Could not discard") == true);
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public void Clear_is_disabled_when_empty_or_callback_is_unavailable()
    {
        var empty = new CapturePickerWindow([], () => { });
        empty.Show();
        try { Assert.False(ButtonNamed(empty, "Clear captures").IsEnabled); }
        finally { empty.Close(); }
        var unavailable = new CapturePickerWindow([new("one", "Capture", DateTimeOffset.Now, 100, 100, Thumbnail(), () => { })]);
        unavailable.Show();
        try { Assert.False(ButtonNamed(unavailable, "Clear captures").IsEnabled); }
        finally { unavailable.Close(); }
    }
}
