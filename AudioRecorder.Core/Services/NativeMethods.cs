using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace AudioRecorder.Core.Services;

public static class NativeMethods
{
    private const string SndVolDllPath = @"%SystemRoot%\System32\SndVolSSO.dll";
    private const int IconIndex = 101;


    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    public static Bitmap? GetSystemSoundIcon()
    {
        var dllPath = Environment.ExpandEnvironmentVariables(SndVolDllPath);

        var iconHandle = ExtractIcon(IntPtr.Zero, dllPath, IconIndex);

        if (iconHandle == IntPtr.Zero) return null;
        using var icon = Icon.FromHandle(iconHandle);

        using var memoryStream = new MemoryStream();
        icon.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new Bitmap(memoryStream);
    }

    public static Bitmap? GetIconForProcess(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            var path = proc.MainModule?.FileName;
            var iconHandle = ExtractAssociatedIcon(IntPtr.Zero, new StringBuilder(path), out var index);

            if (iconHandle != IntPtr.Zero)
            {
                using var icon = Icon.FromHandle(iconHandle);
                using var stream = new MemoryStream();

                icon.ToBitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                DestroyIcon(iconHandle);
                return bitmap;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool DestroyIcon(IntPtr handle);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);
}