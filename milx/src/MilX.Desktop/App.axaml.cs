using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using MilX.Desktop.Services;
using MilX.Desktop.ViewModels;
using MilX.Desktop.Views;

namespace MilX.Desktop;

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
            // MILX_THEME=dark|light forces a theme (diagnostics); otherwise the saved preference applies
            var themeEnv = Environment.GetEnvironmentVariable("MILX_THEME");
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

            // MILX_OPEN=<.odproj | .mdproject | results folder> opens it at startup (also used by smoke tests)
            var autoOpen = Environment.GetEnvironmentVariable("MILX_OPEN");
            if (!string.IsNullOrWhiteSpace(autoOpen))
            {
                window.Opened += async (_, _) =>
                {
                    await vm.OpenAsync(autoOpen);
                    // MILX_EXPORT_OPENQUANT=<csv> writes the annotated spots as OpenQuant components right after opening (smoke tests only)
                    var oqCsv = Environment.GetEnvironmentVariable("MILX_EXPORT_OPENQUANT");
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

            // MILX_IMPORT_OPENQUANT=<.oqproj> imports an OpenQuant batch at start-up (smoke tests only)
            var importOq = Environment.GetEnvironmentVariable("MILX_IMPORT_OPENQUANT");
            if (!string.IsNullOrWhiteSpace(importOq))
            {
                window.Opened += async (_, _) => { await vm.ImportOpenQuantBatchAsync(importOq); Console.WriteLine(vm.Status); Console.WriteLine(vm.Samples.StatusLine); };
            }

            // MILX_OPEN_RAW=<raw file | folder of raw files> adds the files to the batch and opens the first in the Explorer (no results)
            var openRaw = Environment.GetEnvironmentVariable("MILX_OPEN_RAW");
            if (!string.IsNullOrWhiteSpace(openRaw))
            {
                window.Opened += async (_, _) => await vm.OpenRawAsync(openRaw);
            }

            // MILX_AUTORUN=<folder with raw files (+ library.msp, method*.txt)> runs the pipeline at start-up (smoke tests only)
            var autoRun = Environment.GetEnvironmentVariable("MILX_AUTORUN");
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
                    vm.OutputFolder = Path.Combine(autoRun, "milx_output");
                    await vm.ProcessBatchCommand.ExecuteAsync(null);
                };
            }

            // MILX_SNAPSHOT=<png path> renders the window to a PNG a few seconds after start-up (smoke tests only);
            // MILX_SNAPSHOT_TAB=<0..3> selects the workspace first (Explorer, Analytics, Method, Samples).
            var snapshot = Environment.GetEnvironmentVariable("MILX_SNAPSHOT");
            if (!string.IsNullOrWhiteSpace(snapshot))
            {
                Controls.ChartBase.TooltipsEnabled = false;
                window.Opened += async (_, _) =>
                {
                    var delay = int.TryParse(Environment.GetEnvironmentVariable("MILX_SNAPSHOT_DELAY"), out var d) ? d : 10;
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                    try
                    {
                        if (int.TryParse(Environment.GetEnvironmentVariable("MILX_SNAPSHOT_TAB"), out var tab))
                        {
                            vm.SelectedWorkspace = tab;
                            await Task.Delay(2500);
                        }
                        // MILX_SNAPSHOT_ACTION: explorer-peak | analytics-spectrum | analytics-metric | analytics-magnify | wizard | about
                        Avalonia.Controls.Window target = window;
                        switch (Environment.GetEnvironmentVariable("MILX_SNAPSHOT_ACTION"))
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
                                if (tabs is not null) tabs.SelectedIndex = Environment.GetEnvironmentVariable("MILX_SNAPSHOT_ACTION") == "analytics-spectrum" ? 0 : 2;   // MS/MS, Candidates: the peaks sit above the tabs now
                                await Task.Delay(1500);
                                break;
                            }
                            case "stats-cluster":
                            case "stats-network":
                            {
                                // the page by its header, as the probe's selectStatisticsPage does: an index
                                // silently lands on whichever page moved into that slot
                                var statsTabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "StatsTabs");
                                var wantNetwork = Environment.GetEnvironmentVariable("MILX_SNAPSHOT_ACTION") == "stats-network";
                                var pageHeader = wantNetwork ? "Molecular network" : "Dendrogram";
                                var page = statsTabs?.Items.OfType<Avalonia.Controls.TabItem>().FirstOrDefault(t => string.Equals(t.Header as string, pageHeader, StringComparison.OrdinalIgnoreCase));
                                if (statsTabs is not null && page is not null) statsTabs.SelectedItem = page;
                                await Task.Delay(800);
                                if (wantNetwork)
                                {
                                    await vm.Statistics.BuildNetworkCommand.ExecuteAsync(null);
                                    Console.WriteLine("[stats] " + vm.Statistics.NetworkLabel);
                                }
                                await Task.Delay(2500);
                                break;
                            }
                            case "search-demo":
                            {
                                // Runs a real library re-search through the same command the button uses.
                                await Task.Delay(2000);
                                Console.WriteLine($"[search] {vm.Analytics.IonRows.Count} feature(s) in the table");
                                var pick = vm.Analytics.IonRows.Where(r => r.Spot.Candidates.Count > 0).OrderByDescending(r => r.Height).FirstOrDefault()
                                           ?? vm.Analytics.IonRows.Where(r => r.Mz > 400).OrderByDescending(r => r.Height).FirstOrDefault()
                                           ?? vm.Analytics.IonRows.FirstOrDefault();
                                if (pick is null) { Console.WriteLine("[search] no feature"); break; }
                                vm.Analytics.SelectedRow = pick;
                                await Task.Delay(3000);
                                Console.WriteLine($"[search] feature #{pick.Id} {pick.DisplayName} RT {pick.Rt:F3} m/z {pick.Mz:F4}");
                                Console.WriteLine($"[search] the run kept {vm.Analytics.Candidates.Count} match(es):");
                                foreach (var c in vm.Analytics.Candidates.Take(5))
                                {
                                    Console.WriteLine($"[search]   {c.Name,-40} total {c.TotalScore,6:F3} dot {c.WeightedDotProduct,6:F3} rev {c.ReverseDotProduct,6:F3}{(c.IsRepresentative ? "  <- reported" : string.Empty)}");
                                }
                                vm.Analytics.SearchMs1Tolerance = "0.05";
                                vm.Analytics.SearchMs2Tolerance = "0.05";
                                Console.WriteLine("[search] searching the library again at 0.05 Da on MS1");
                                await vm.Analytics.SearchLibraryCommand.ExecuteAsync(null);
                                Console.WriteLine($"[search] {vm.Analytics.SearchHint}");
                                foreach (var c in vm.Analytics.Candidates.Take(8))
                                {
                                    Console.WriteLine($"[search]   {c.Name,-40} total {c.TotalScore,6:F3} dot {c.WeightedDotProduct,6:F3} rev {c.ReverseDotProduct,6:F3} matched {c.MatchedPeaksCount,4:F0}");
                                }
                                var searchTabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
                                if (searchTabs is not null) searchTabs.SelectedIndex = 2;   // Candidates
                                await Task.Delay(2000);
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
                                if (tabs is not null) tabs.SelectedIndex = 4;   // Feature map
                                await Task.Delay(1800);
                                break;
                            }
                            case "review-candidates":
                            {
                                vm.Analytics.AnnotationFilter = "Confident";
                                await Task.Delay(1200);
                                var tabs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
                                if (tabs is not null) tabs.SelectedIndex = 2;   // Candidates
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
