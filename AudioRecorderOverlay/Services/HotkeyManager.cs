using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Win32.Input;

namespace AudioRecorderOverlay.Services;

public static class HotkeyManager
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static Thread? _hotkeyThread;
    private static Action? _callback;
    private const int HotkeyId = 1;

    public static void RegisterOverlayHotkey(Action callback)
    {
        _callback = callback;

        _hotkeyThread = new Thread(() =>
        {
            using var source = new Win32HotkeyWindow();
            // Alt + Shift + S
            RegisterHotKey(source.Handle, HotkeyId, 0x0001 | 0x0004, (uint)KeyInterop.VirtualKeyFromKey(Key.S));

            while (source.ProcessMessages())
            {

            }

            UnregisterHotKey(source.Handle, HotkeyId);
        });

        _hotkeyThread.SetApartmentState(ApartmentState.STA);
        _hotkeyThread.IsBackground = true;
        _hotkeyThread.Start();
    }

    public static void UnregisterHotkey()
    {
        _hotkeyThread?.Interrupt();
        _hotkeyThread = null;
    }

    private class Win32HotkeyWindow : IDisposable
    {
        private readonly IntPtr _hwnd;

        public IntPtr Handle => _hwnd;

        public Win32HotkeyWindow()
        {
            _hwnd = CreateMessageWindow();
        }

        public bool ProcessMessages()
        {
            var msg = new MSG();
            while (GetMessage(ref msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WmHotkey && (int)msg.wParam == HotkeyId && _callback != null)
                {
                    Dispatcher.UIThread.Post(_callback);
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            return true;
        }

        public void Dispose()
        {
            DestroyWindow(_hwnd);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(
            int exStyle, string className, string windowName,
            int style, int x, int y, int width, int height,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        private static IntPtr CreateMessageWindow()
        {
            return CreateWindowEx(0, "Static", "Message", 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpmsg);

        private const int WmHotkey = 0x0312;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int x;
    public int y;
}
