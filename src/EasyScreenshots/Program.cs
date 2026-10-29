using Avalonia;

namespace EasyScreenshots;

internal static class Program
{
    private static Mutex? _instance;
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--native-smoke")
        {
            Environment.ExitCode = Platform.NativeSmoke.Run(args[1]);
            return;
        }
        _instance = new Mutex(true, @"Local\EasyScreenshots.SingleInstance", out var first);
        if (!first) return;
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        finally { _instance.ReleaseMutex(); _instance.Dispose(); }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
