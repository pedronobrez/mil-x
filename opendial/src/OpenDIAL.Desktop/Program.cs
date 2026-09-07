using System.Globalization;
using Avalonia;

namespace OpenDIAL.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // MS-DIAL parses numbers with the current culture everywhere; anything but the invariant
        // culture silently produces zero peaks on machines with a comma decimal separator.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        if (Environment.GetEnvironmentVariable("OPENDIAL_TRACE") == "1")
        {
            // surfaces Avalonia binding/layout warnings on the console (diagnostics only)
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
