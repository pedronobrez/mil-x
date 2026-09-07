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
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
