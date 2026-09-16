using System.Diagnostics;

namespace MilX.Desktop.Services;

/// <summary>Opens files/folders with the operating system's default handler.</summary>
public static class ShellService
{
    public static void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", new[] { path }) { UseShellExecute = false });
            }
            else if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer", new[] { path }) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("xdg-open", new[] { path }) { UseShellExecute = false });
            }
        }
        catch
        {
            // best effort
        }
    }

    public static void Reveal(string path)
    {
        if (OperatingSystem.IsMacOS() && File.Exists(path))
        {
            try { Process.Start(new ProcessStartInfo("open", new[] { "-R", path }) { UseShellExecute = false }); return; } catch { }
        }
        Open(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
    }
}
