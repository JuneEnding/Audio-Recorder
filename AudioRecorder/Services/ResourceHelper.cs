using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace AudioRecorder.Services;

public static class ResourceHelper
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int LoadString(IntPtr hInstance, uint uId, System.Text.StringBuilder lpBuffer, int nBufferMax);

    public static Bitmap? GetBitmapFromIconPath(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        var parts = iconPath.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var resourceId))
            return null;

        var dllPath = Environment.ExpandEnvironmentVariables(parts[0].Trim('@'));
        var hInstance = LoadLibrary(dllPath);

        if (hInstance == IntPtr.Zero)
            return null;

        try
        {
            var hIcon = LoadIcon(hInstance, (IntPtr)(-resourceId));
            if (hIcon == IntPtr.Zero)
                return null;

            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var memoryStream = new MemoryStream();
            icon.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return new Bitmap(memoryStream);
        }
        finally
        {
            DestroyIcon(hInstance);
        }
    }

    public static string GetLocalizedStringFromResource(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return string.Empty;

        var parts = resourcePath.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var resourceId))
            return string.Empty;

        var dllPath = Environment.ExpandEnvironmentVariables(parts[0].Trim('@'));
        var hInstance = LoadLibrary(dllPath);

        if (hInstance == IntPtr.Zero)
            return string.Empty;

        var buffer = new System.Text.StringBuilder(256);
        LoadString(hInstance, (uint)(-resourceId), buffer, buffer.Capacity);

        return buffer.ToString();
    }
}
