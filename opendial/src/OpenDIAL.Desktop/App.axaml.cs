using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;

namespace OpenDIAL.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService();
            var window = new MainWindow();
            var dialogs = new FileDialogService(window);
            var vm = new MainWindowViewModel(settings, dialogs);
            window.DataContext = vm;
            desktop.MainWindow = window;

            // OPENDIAL_OPEN=<folder or .mdproject> opens existing results at startup (also used by smoke tests)
            var autoOpen = Environment.GetEnvironmentVariable("OPENDIAL_OPEN");
            if (!string.IsNullOrWhiteSpace(autoOpen))
            {
                window.Opened += async (_, _) => await vm.OpenResultsAsync(autoOpen);
            }

            // OPENDIAL_AUTORUN=<folder with raw files (+ library.msp, method*.txt)> runs the pipeline at start-up (smoke tests only)
            var autoRun = Environment.GetEnvironmentVariable("OPENDIAL_AUTORUN");
            if (!string.IsNullOrWhiteSpace(autoRun) && Directory.Exists(autoRun))
            {
                window.Opened += async (_, _) =>
                {
                    var method = Directory.EnumerateFiles(autoRun, "method*.txt").FirstOrDefault();
                    if (method is not null)
                    {
                        vm.Project.Parameters.Load(await Pipeline.Parameters.MethodParameters.LoadAsync(method));
                    }
                    var msp = Path.Combine(autoRun, "library.msp");
                    if (File.Exists(msp))
                    {
                        vm.Project.Parameters.MspFilePath = msp;
                    }
                    vm.Project.AddPaths(Pipeline.Vendor.FileFormats.EnumerateRawFiles(autoRun));
                    vm.Project.OutputFolder = Path.Combine(autoRun, "opendial_output");
                    await vm.RunPipelineCommand.ExecuteAsync(null);
                };
            }

            // OPENDIAL_SNAPSHOT=<png path> renders the window to a PNG a few seconds after start-up (smoke tests only);
            // OPENDIAL_SNAPSHOT_TAB=<n> selects the n-th tab of the results page first.
            var snapshot = Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT");
            if (!string.IsNullOrWhiteSpace(snapshot))
            {
                window.Opened += async (_, _) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    try
                    {
                        if (int.TryParse(Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT_TAB"), out var tab))
                        {
                            var tabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultsTabs");
                            if (tabs is not null)
                            {
                                tabs.SelectedIndex = tab;
                                await Task.Delay(1500);
                            }
                        }
                        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96, 96));
                        bitmap.Render(window);
                        bitmap.Save(snapshot);
                        Console.WriteLine($"Snapshot written to {snapshot}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Snapshot failed: " + ex);
                    }
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
