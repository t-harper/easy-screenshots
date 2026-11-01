using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

namespace EasyScreenshots.Platform;

/// <summary>Exercises real native APIs without opening windows or saving captured pixels.</summary>
public static class NativeSmoke
{
    public static int Run(string outputPath)
    {
        var checks = new Dictionary<string, object?>
        {
            ["utc"] = DateTimeOffset.UtcNow,
            ["os"] = Environment.OSVersion.ToString(),
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString()
        };
        var result = 1;
        IntPtr previousDpi = IntPtr.Zero;
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows 11 is required.");
            // This path runs before Avalonia initializes. Explicitly use physical pixel APIs
            // even when invoked by dotnet.exe, whose manifest belongs to the host.
            previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
            if (previousDpi == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot enter PerMonitorV2 DPI mode.");
            checks["perMonitorV2"] = AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4));
            if (!(bool)checks["perMonitorV2"]!) throw new InvalidOperationException("PerMonitorV2 was not applied.");
            using (var first = new GlobalHotkey(() => { }))
            {
                checks["hotkeyRegistered"] = true;
                var rejected = false;
                try { using var duplicate = new GlobalHotkey(() => { }); }
                catch (Win32Exception ex)
                {
                    rejected = true;
                    checks["duplicateRegistrationError"] = ex.NativeErrorCode;
                }
                if (!rejected) throw new InvalidOperationException("Duplicate shortcut registration unexpectedly succeeded.");
                checks["duplicateRejected"] = true;
                using var snapshot = DesktopCapture.Capture();
                var capturedPng = snapshot.Png;
                using var decoded = SKBitmap.Decode(capturedPng)
                    ?? throw new InvalidOperationException("Captured PNG could not be decoded.");
                if (decoded.Width != snapshot.Width || decoded.Height != snapshot.Height)
                    throw new InvalidOperationException("Captured PNG size does not match native desktop bounds.");
                if (decoded.GetPixel(0, 0).Alpha != 255)
                    throw new InvalidOperationException("Captured PNG should be opaque.");
                checks["capture"] = new
                {
                    snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height,
                    pngBytes = capturedPng.Length,
                    decoded = true,
                    opaque = true
                };
            }
            using (var registeredAgain = new GlobalHotkey(() => { }))
                checks["hotkeyReleasedAndRegisteredAgain"] = true;
            checks["passed"] = true;
            result = 0;
        }
        catch (Exception ex)
        {
            checks["passed"] = false;
            checks["error"] = ex.ToString();
        }
        finally
        {
            if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi);
        }
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}
