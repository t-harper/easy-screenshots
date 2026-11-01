using EasyScreenshots.Core;
using Xunit;

namespace EasyScreenshots.Tests;

public class HotkeyTests
{
    [Fact] public void ExactWinShiftFourTriggersOnceAndConsumesRepeatsAndRelease()
    {
        var filter = new WindowsShortcutFilter();
        Assert.Equal(new ShortcutDecision(true, true), filter.Process(0x34, true, true, true, false, false));
        Assert.Equal(new ShortcutDecision(true, false), filter.Process(0x34, true, true, true, false, false));
        Assert.Equal(new ShortcutDecision(true, false), filter.Process(0x34, false, false, false, false, false));
        Assert.Equal(new ShortcutDecision(true, true), filter.Process(0x34, true, true, true, false, false));
    }

    [Theory]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    public void OtherModifierCombinationsPassThrough(bool win, bool shift, bool ctrl, bool alt)
    {
        var filter = new WindowsShortcutFilter();
        Assert.Equal(default, filter.Process(0x34, true, win, shift, ctrl, alt));
        Assert.Equal(default, filter.Process(0x34, false, win, shift, ctrl, alt));
    }

    [Fact] public void OtherKeysAndUnmatchedReleasePassThrough()
    {
        var filter = new WindowsShortcutFilter();
        Assert.Equal(default, filter.Process(0x35, true, true, true, false, false));
        Assert.Equal(default, filter.Process(0x34, false, true, true, false, false));
        filter.Process(0x34, true, true, true, false, false);
        Assert.Equal(default, filter.Process(0x5B, false, false, true, false, false));
        Assert.Equal(new ShortcutDecision(true, false), filter.Process(0x34, false, false, true, false, false));
    }

    [Fact] public void CancelResetsSuppressionState()
    {
        var filter = new WindowsShortcutFilter();
        filter.Process(0x34, true, true, true, false, false);
        filter.Cancel();
        Assert.Equal(default, filter.Process(0x34, false, false, false, false, false));
        Assert.Equal(new ShortcutDecision(true, true), filter.Process(0x34, true, true, true, false, false));
    }

    [Fact] public void AddingModifiersToAlreadyHeldFourDoesNotTrigger()
    {
        var filter = new WindowsShortcutFilter();
        Assert.Equal(default, filter.Process(0x34, true, false, false, false, false));
        Assert.Equal(default, filter.Process(0x34, true, true, true, false, false));
        Assert.Equal(default, filter.Process(0x34, false, true, true, false, false));
        Assert.Equal(new ShortcutDecision(true, true), filter.Process(0x34, true, true, true, false, false));
    }
}
