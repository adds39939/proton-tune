using System.Runtime.InteropServices;

namespace ProtonTune.App;

/// <summary>
/// Configures the WebKitGTK renderer that backs Photino's window on Linux.
/// </summary>
internal static class WebKitEnvironment
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setenv(string name, string value, int overwrite);

    /// <summary>
    /// Falls WebKitGTK back to its software renderer. The DMA-BUF renderer sends buffer
    /// messages that GTK3's Wayland backend rejects, killing the window with Gdk "Error 71".
    /// Must be called before the window is created, and goes through libc because
    /// <see cref="Environment.SetEnvironmentVariable(string, string)"/> only updates the CLR's
    /// managed copy on Unix — the native library would never see it.
    /// </summary>
    public static void DisableDmaBufRenderer() => setenv("WEBKIT_DISABLE_DMABUF_RENDERER", "1", 1);

    /// <summary>
    /// Ensures the application is running on Linux.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The current OS is not Linux.</exception>
    public static void EnsureOsIsLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This application is only supported on Linux");
        }
    }
}
