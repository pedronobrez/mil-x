using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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
            // OPENDIAL_THEME=dark|light forces a theme (diagnostics); otherwise the saved preference applies
            var themeEnv = Environment.GetEnvironmentVariable("OPENDIAL_THEME");
            if (!string.IsNullOrWhiteSpace(themeEnv))
            {
                RequestedThemeVariant = themeEnv.Equals("dark", StringComparison.OrdinalIgnoreCase) ? ThemeVariant.Dark : themeEnv.Equals("light", StringComparison.OrdinalIgnoreCase) ? ThemeVariant.Light : ThemeVariant.Default;
            }
            else
            {
                SettingsWindow.ApplyTheme(settings.Current.Theme);
            }

            var window = new MainWindow();
            var dialogs = new FileDialogService(window);
            var messages = new MessageService(window);
            var vm = new MainWindowViewModel(settings, dialogs, messages);
            window.DataContext = vm;
            desktop.MainWindow = window;

            // OPENDIAL_OPEN=<.odproj | .mdproject | results folder> opens it at startup (also used by smoke tests)
            var autoOpen = Environment.GetEnvironmentVariable("OPENDIAL_OPEN");
            if (!string.IsNullOrWhiteSpace(autoOpen))
            {
                window.Opened += async (_, _) =>
                {
                    await vm.OpenAsync(autoOpen);
                    // OPENDIAL_EXPORT_OPENQUANT=<csv> writes the annotated spots as OpenQuant components right after opening (smoke tests only)
                    var oqCsv = Environment.GetEnvironmentVariable("OPENDIAL_EXPORT_OPENQUANT");
                    if (!string.IsNullOrWhiteSpace(oqCsv) && vm.Results is not null)
                    {
                        try
                        {
                            var n = await vm.Analytics.ExportOpenQuantAsync(oqCsv, new Interop.OpenQuant.OpenQuantExportOptions { AnnotatedOnly = true });
                            vm.Status = $"{n} components written to {oqCsv}";
                            Console.WriteLine(vm.Status);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine("OpenQuant export failed: " + ex);
                        }
                    }
                };
            }

            // OPENDIAL_IMPORT_OPENQUANT=<.oqproj> imports an OpenQuant batch at start-up (smoke tests only)
            var importOq = Environment.GetEnvironmentVariable("OPENDIAL_IMPORT_OPENQUANT");
            if (!string.IsNullOrWhiteSpace(importOq))
            {
                window.Opened += async (_, _) => { await vm.ImportOpenQuantBatchAsync(importOq); Console.WriteLine(vm.Status); Console.WriteLine(vm.Samples.StatusLine); };
            }

            // OPENDIAL_OPEN_RAW=<raw file | folder of raw files> adds the files to the batch and opens the first in the Explorer (no results)
            var openRaw = Environment.GetEnvironmentVariable("OPENDIAL_OPEN_RAW");
            if (!string.IsNullOrWhiteSpace(openRaw))
            {
                window.Opened += async (_, _) => await vm.OpenRawAsync(openRaw);
            }

            // OPENDIAL_AUTORUN=<folder with raw files (+ library.msp, method*.txt)> runs the pipeline at start-up (smoke tests only)
            var autoRun = Environment.GetEnvironmentVariable("OPENDIAL_AUTORUN");
            if (!string.IsNullOrWhiteSpace(autoRun) && Directory.Exists(autoRun))
            {
                window.Opened += async (_, _) =>
                {
                    var method = Directory.EnumerateFiles(autoRun, "method*.txt").FirstOrDefault();
                    var mode = Pipeline.Model.IonizationMode.LCMS;
                    if (method is not null)
                    {
                        var text = await File.ReadAllTextAsync(method);
                        if (text.Contains("Machine category: GCMS", StringComparison.OrdinalIgnoreCase)) mode = Pipeline.Model.IonizationMode.GCMS;
                        vm.Method.LoadText(text, mode);
                    }
                    var msp = Path.Combine(autoRun, "library.msp");
                    if (File.Exists(msp)) vm.Method.Parameters.MspFilePath = msp;
                    vm.Samples.AddPaths(Pipeline.Vendor.FileFormats.EnumerateRawFiles(autoRun));
                    vm.OutputFolder = Path.Combine(autoRun, "opendial_output");
                    await vm.ProcessBatchCommand.ExecuteAsync(null);
                };
            }

            // OPENDIAL_SNAPSHOT=<png path> renders the window to a PNG a few seconds after start-up (smoke tests only);
            // OPENDIAL_SNAPSHOT_TAB=<0..3> selects the workspace first (Explorer, Analytics, Method, Samples).
            var snapshot = Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT");
            if (!string.IsNullOrWhiteSpace(snapshot))
            {
                Controls.ChartBase.TooltipsEnabled = false;
                window.Opened += async (_, _) =>
                {
                    var delay = int.TryParse(Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT_DELAY"), out var d) ? d : 10;
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                    try
                    {
                        if (int.TryParse(Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT_TAB"), out var tab))
                        {
                            vm.SelectedWorkspace = tab;
                            await Task.Delay(2500);
                        }
                        // OPENDIAL_SNAPSHOT_ACTION: explorer-peak | analytics-spectrum | analytics-metric | analytics-magnify | wizard | about
                        Avalonia.Controls.Window target = window;
                        switch (Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT_ACTION"))
                        {
                            case "explorer-peak":
                                vm.Explorer.SelectedPeak = vm.Explorer.Peaks.FirstOrDefault(p => p.IsAnnotated);
                                await Task.Delay(2500);
                                break;
                            case "explorer-channel":
                            {
                                // expand the first file and tick its first product-ion (MS2) channel next to the TIC
                                var file = vm.Explorer.Tree.FirstOrDefault();
                                if (file is not null) file.IsExpanded = true;
                                var leaf = vm.Explorer.ActiveChannels.FirstOrDefault(n => n.Channel is { Kind: Pipeline.Results.RawChannelKind.Ms2Window })
                                           ?? vm.Explorer.ActiveChannels.FirstOrDefault(n => n.Channel is { Kind: Pipeline.Results.RawChannelKind.Ms2Events });
                                if (leaf is not null) { leaf.IsChecked = true; vm.Explorer.ActiveChannel = leaf; }
                                vm.Explorer.Normalize = true;
                                await Task.Delay(2500);
                                // show the product-ion spectrum at the apex of the first drawn trace
                                var trace = vm.Explorer.Series.FirstOrDefault();
                                if (trace is { Points.Count: > 0 }) vm.Explorer.ShowScanAt(trace.Points.MaxBy(p => p.Y).X);
                                await Task.Delay(1500);
                                break;
                            }
                            case "analytics-spectrum":
                            case "analytics-metric":
                            {
                                var tabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
                                if (tabs is not null) tabs.SelectedIndex = Environment.GetEnvironmentVariable("OPENDIAL_SNAPSHOT_ACTION") == "analytics-spectrum" ? 1 : 3;
                                await Task.Delay(1500);
                                break;
                            }
                            case "analytics-magnify":
                                vm.Analytics.Magnify(vm.Analytics.Panels.FirstOrDefault());
                                await Task.Delay(1500);
                                break;
                            case "wizard":
                            {
                                var w = new NewProjectWindow(new NewProjectViewModel(settings, dialogs)) { WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen };
                                w.Show(window);
                                await Task.Delay(1500);
                                target = w;
                                break;
                            }
                            case "about":
                            {
                                var w = new AboutWindow();
                                w.Show(window);
                                await Task.Delay(1500);
                                target = w;
                                break;
                            }
                        }
                        var size = new PixelSize((int)target.Bounds.Width, (int)target.Bounds.Height);
                        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96, 96));
                        bitmap.Render(target);
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
