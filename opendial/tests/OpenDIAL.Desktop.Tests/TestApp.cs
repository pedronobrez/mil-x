using Avalonia;
using Avalonia.Headless;
using OpenDIAL.Desktop;

[assembly: AvaloniaTestApplication(typeof(OpenDIAL.Desktop.Tests.TestApp))]

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// Builds the real application for the headless tests, so the styles, the theme and the compiled
/// XAML are the ones that ship rather than a stand-in.
/// </summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            // The same font the application ships with. Without this the headless tests drew with
            // whatever face the machine offered first, so a stored frame described that machine
            // rather than the application, and the monospace columns moved between them.
            .WithInterFont()
            // real drawing, so a test can capture the frame and compare it with a stored one
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
