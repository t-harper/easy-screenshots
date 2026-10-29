using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace EasyScreenshots.Platform;

/// <summary>Owns registration and its native message queue on a dedicated thread.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0x5343;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private Exception? _error;
    private uint _threadId;
    private int _disposed;

    public GlobalHotkey(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _thread = new Thread(() => Run(callback)) { IsBackground = true, Name = "Screenshot hotkey" };
        _thread.Start();
        _ready.Wait();
        if (_error is not null)
        {
            _thread.Join();
            _ready.Dispose();
            throw _error;
        }
    }

    private void Run(Action callback)
    {
        _threadId = GetCurrentThreadId();
        // Ensure a message queue exists before another thread can post WM_QUIT.
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, 0x0002 | 0x0004 | 0x4000, 0x34))
        {
            _error = new Win32Exception(Marshal.GetLastWin32Error(),
                "Ctrl+Shift+4 could not be registered. Another application may already use it.");
            _ready.Set();
            return;
        }
        _ready.Set();
        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Message == 0x0312 && message.WParam == (UIntPtr)HotkeyId)
                    Dispatcher.UIThread.Post(() => { if (Volatile.Read(ref _disposed) == 0) callback(); });
        }
        finally { UnregisterHotKey(IntPtr.Zero, HotkeyId); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        PostThreadMessage(_threadId, 0x0012, UIntPtr.Zero, IntPtr.Zero);
        _thread.Join();
        _ready.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out NativeMessage message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint id, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
