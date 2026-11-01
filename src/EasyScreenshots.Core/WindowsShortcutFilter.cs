namespace EasyScreenshots.Core;

public readonly record struct ShortcutDecision(bool Consume, bool Trigger);

/// <summary>State for the exact Win+Shift+4 override. It retains no keyboard input.</summary>
public sealed class WindowsShortcutFilter
{
    private bool _fourDown;
    private bool _suppressFour;

    public ShortcutDecision Process(uint virtualKey, bool isDown, bool win, bool shift, bool ctrl, bool alt)
    {
        if (virtualKey != 0x34) return default;
        if (!isDown)
        {
            var consume = _suppressFour;
            Cancel();
            return new(consume, false);
        }
        if (_fourDown) return new(_suppressFour, false);
        _fourDown = true;
        _suppressFour = win && shift && !ctrl && !alt;
        return new(_suppressFour, _suppressFour);
    }

    public void Cancel() { _fourDown = false; _suppressFour = false; }
}
