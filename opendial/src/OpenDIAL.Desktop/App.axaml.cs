using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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

            // Documents opened from Finder arrive either as launch arguments (cold start) or as a
            // file-activation event (the application is already running); both end in OpenAnyAsync.
            var launchPath = (desktop.Args ?? Array.Empty<string>())
                .FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal) && (File.Exists(a) || Directory.Exists(a)));
            if (launchPath is not null)
            {
                window.Opened += async (_, _) => await vm.OpenAnyAsync(launchPath);
            }
            if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            {
                activatable.Activated += async (_, e) =>
                {
                    if (e is not FileActivatedEventArgs activation) return;
                    var path = activation.Files.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => !string.IsNullOrEmpty(p));
                    if (path is not null) await vm.OpenAnyAsync(path);
                };
            }

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
                            case "reintegrate-demo":
                            {
                                // Drives a real re-integration through the same commands the buttons
                                // use, and prints what changed so the run can be checked from outside.
                                vm.Analytics.AnnotationFilter = "Confident";
                                await Task.Delay(1500);
                                var demoRow = vm.Analytics.IonRows.OrderByDescending(r => r.Score).FirstOrDefault();
                                if (demoRow is null) { Console.WriteLine("[demo] no annotated feature"); break; }
                                vm.Analytics.SelectedRow = demoRow;
                                for (var wait = 0; wait < 120 && (vm.Analytics.IsGridBusy || vm.Analytics.Panels.Count == 0); wait++)
                                {
                                    await Task.Delay(2000);   // let every sample's chromatogram load
                                }
                                var spot = demoRow.Spot;
                                Console.WriteLine($"[demo] feature #{demoRow.Id} {demoRow.DisplayName} RT {demoRow.Rt:F3} m/z {demoRow.Mz:F4}");
                                Console.WriteLine($"[demo] before: mean height {spot.AverageHeight:N0}, fill {spot.FillPercent:F0} %");
                                foreach (var p in spot.SamplePeaks)
                                {
                                    Console.WriteLine($"[demo]   {p.FileName,-28} height {p.Height,12:N0}  area {p.Area,14:N0}  window {p.RtLeft:F3}-{p.RtRight:F3}");
                                }
                                var half = spot.SamplePeaks.FirstOrDefault(p => p.HasPeak);
                                if (half is null) { Console.WriteLine("[demo] no detected peak"); break; }
                                var centre = half.Rt;
                                var width = Math.Max(0.02, (half.RtRight - half.RtLeft) / 4.0);
                                vm.Analytics.SetIntegrationWindow(centre - width, centre + width);
                                Console.WriteLine($"[demo] applying window {centre - width:F3}-{centre + width:F3} min to every sample");
                                await vm.Analytics.ReintegrateAllCommand.ExecuteAsync(null);
                                await Task.Delay(2500);
                                var after = vm.Analytics.SelectedRow?.Spot;
                                if (after is not null)
                                {
                                    Console.WriteLine($"[demo] after : mean height {after.AverageHeight:N0}, fill {after.FillPercent:F0} %");
                                    foreach (var p in after.SamplePeaks)
                                    {
                                        Console.WriteLine($"[demo]   {p.FileName,-28} height {p.Height,12:N0}  area {p.Area,14:N0}  window {p.RtLeft:F3}-{p.RtRight:F3}");
                                    }
                                }
                                Console.WriteLine($"[demo] status: {vm.Analytics.Summary}");
                                vm.Analytics.SaveCurationCommand.Execute(null);
                                await Task.Delay(4000);
                                Console.WriteLine($"[demo] save  : {vm.Analytics.Summary}");
                                break;
                            }
                            case "review-peaks":
                            {
                                // an annotated feature with the integrate strip in view
                                vm.Analytics.AnnotationFilter = "Confident";
                                await Task.Delay(2500);
                                break;
                            }
                            case "review-map":
                            {
                                // the review workspace with a class filter on and the feature map open
                                vm.Analytics.AnnotationFilter = "Annotated";
                                await Task.Delay(1200);
                                var tabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
                                if (tabs is not null) tabs.SelectedIndex = 5;
                                await Task.Delay(1800);
                                break;
                            }
                            case "review-candidates":
                            {
                                vm.Analytics.AnnotationFilter = "Confident";
                                await Task.Delay(1200);
                                var tabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
                                if (tabs is not null) tabs.SelectedIndex = 3;
                                await Task.Delay(1800);
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
