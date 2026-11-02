using Avalonia.Controls;
using Avalonia.Platform;

namespace EasyScreenshots;

public static class AppIcon
{
    public static WindowIcon Default { get; } = Load();

    private static WindowIcon Load()
    {
        using var stream = AssetLoader.Open(new Uri("avares://EasyScreenshots/Assets/app.ico"));
        return new WindowIcon(stream);
    }
}
