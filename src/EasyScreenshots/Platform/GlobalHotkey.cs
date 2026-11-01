using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using EasyScreenshots.Core;

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
    private readonly Action _callback;
    private readonly KeyboardHookProc _hookProc;
    private readonly WindowsShortcutFilter _filter = new();
    private IntPtr _keyboardHook;

    public GlobalHotkey(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
        _hookProc = KeyboardHook;
        _thread = new Thread(Run) { IsBackground = true, Name = "Screenshot hotkey" };
        _thread.Start();
        _ready.Wait();
        if (_error is not null)
        {
            _thread.Join();
            _ready.Dispose();
            throw _error;
        }
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        // Ensure a message queue exists before another thread can post WM_QUIT.
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        var registered = false;
        try
        {
            if (!RegisterHotKey(IntPtr.Zero, HotkeyId, 0x0002 | 0x0004 | 0x4000, 0x34))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Ctrl+Shift+4 could not be registered. Another application may already use it.");
            registered = true;
            // Win+Shift+4 is already registered by Windows on this machine. A narrow
            // hook consumes only this exact chord; Ctrl+Shift+4 stays a normal hotkey.
            _keyboardHook = SetWindowsHookEx(13, _hookProc, GetModuleHandle(null), 0);
            if (_keyboardHook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Win+Shift+4 capture could not be enabled.");
            _ready.Set();
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Message == 0x0312 && message.WParam == (UIntPtr)HotkeyId)
                    DispatchCapture();
        }
        catch (Exception ex) { _error = ex; _ready.Set(); }
        finally
        {
            if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _filter.Cancel();
            if (registered) UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }
    }

    private void DispatchCapture() => Dispatcher.UIThread.Post(() => { if (Volatile.Read(ref _disposed) == 0) _callback(); });

    private IntPtr KeyboardHook(int code, UIntPtr message, IntPtr data)
    {
        if (code < 0 || Volatile.Read(ref _disposed) != 0) return CallNextHookEx(_keyboardHook, code, message, data);
        var id = message.ToUInt64();
        if (id is not (0x0100 or 0x0101 or 0x0104 or 0x0105)) return CallNextHookEx(_keyboardHook, code, message, data);
        var key = Marshal.PtrToStructure<KeyboardInput>(data);
        var decision = _filter.Process(key.VirtualKey, id is 0x0100 or 0x0104,
            IsDown(0x5B) || IsDown(0x5C), IsDown(0x10), IsDown(0x11), IsDown(0x12));
        if (!decision.Consume) return CallNextHookEx(_keyboardHook, code, message, data);
        if (decision.Trigger) DispatchCapture();
        return new IntPtr(1);
    }

    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        PostThreadMessage(_threadId, 0x0012, UIntPtr.Zero, IntPtr.Zero);
        _thread.Join();
        _ready.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public uint VirtualKey, ScanCode, Flags, Time; public UIntPtr ExtraInfo; }
    private delegate IntPtr KeyboardHookProc(int code, UIntPtr message, IntPtr data);
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
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, KeyboardHookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
