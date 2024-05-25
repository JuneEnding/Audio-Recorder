using AudioRecorder.Models;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace YourAppName.Services
{
    internal static class NativeMethods
    {
        public static readonly Int32 GWL_STYLE = -16;
        public static readonly UInt64 WS_VISIBLE = 0x10000000L;
        public static readonly UInt64 WS_BORDER = 0x00800000L;
        public static readonly UInt64 DESIRED_WS = WS_BORDER | WS_VISIBLE;

        public delegate Boolean EnumWindowsCallback(IntPtr hwnd, Int32 lParam);

        public static List<WindowWrapper> GetAllWindows()
        {
            var windows = new List<WindowWrapper>();
            var buffer = new StringBuilder(100);
            EnumWindows(delegate (IntPtr hwnd, Int32 lParam)
            {
                if ((GetWindowLongA(hwnd, GWL_STYLE) & DESIRED_WS) == DESIRED_WS)
                {
                    GetWindowText(hwnd, buffer, buffer.Capacity);
                    uint processId;
                    GetWindowThreadProcessId(hwnd, out processId);

                    var wnd = new WindowWrapper()
                    {
                        handle = hwnd,
                        title = buffer.ToString(),
                        processId = (int)processId,
                        icon = GetIconForProcess((int)processId)
                    };

                    windows.Add(wnd);
                }
                return true;
            }, 0);

            return windows;
        }

        public static Avalonia.Media.Imaging.Bitmap? GetIconForProcess(int processId)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                var path = proc.MainModule?.FileName;
                ushort index = 0;
                IntPtr iconHandle = ExtractAssociatedIcon(IntPtr.Zero, new StringBuilder(path), out index);
                if (iconHandle != IntPtr.Zero)
                {
#if WINDOWS
#pragma warning disable CA1416
                    using (var icon = Icon.FromHandle(iconHandle))
                    {
                        using (var stream = new MemoryStream())
                        {
                            icon.ToBitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            stream.Position = 0;
                            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                            DestroyIcon(iconHandle);
                            return bitmap;
                        }
                    }
#pragma warning restore CA1416
#endif
                }
            }
            catch
            {

            }
            return null;
        }

        [DllImport("user32.dll")]
        static extern Int32 EnumWindows(EnumWindowsCallback lpEnumFunc, Int32 lParam);

        [DllImport("user32.dll")]
        public static extern void GetWindowText(IntPtr hWnd, StringBuilder lpString, Int32 nMaxCount);

        [DllImport("user32.dll")]
        static extern UInt64 GetWindowLongA(IntPtr hWnd, Int32 nIndex);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);
    }
}
