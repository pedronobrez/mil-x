using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// Renders a workspace and compares the frame with a stored one.
///
/// The headless tests catch a broken binding but not a broken layout: a panel that collapses to
/// nothing, a strip that overflows, a column that clips its own text all bind perfectly and look
/// wrong. These catch that.
///
/// The frames are meant to travel. Both fonts the interface uses are carried in the application
/// rather than borrowed from the machine — Inter for the text, JetBrains Mono for the columns of
/// numbers — and the test application builds with the same font stack the real one does, so the
/// layout that is captured here is the layout everywhere.
///
/// What is left is sub-pixel: hinting and anti-aliasing still differ a little between machines and
/// Skia versions. So the comparison is made on a coarsened copy of both frames, three pixels to
/// one, which throws that away and keeps everything that moved, resized or disappeared. Set
/// OPENDIAL_UPDATE_BASELINES=1 to write the current frames as the new reference, and look at the
/// result before committing it.
/// </summary>
public class VisualRegressionTests
{
    private static string BaselineDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Baselines");

    private static string SourceBaselineDirectory
    {
        get
        {
            // walk up to the test project so an update writes where the files are kept, not into bin
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenDIAL.Desktop.Tests.csproj")))
            {
                dir = dir.Parent;
            }
            return dir is null ? BaselineDirectory : Path.Combine(dir.FullName, "Baselines");
        }
    }

    private static bool Updating => Environment.GetEnvironmentVariable("OPENDIAL_UPDATE_BASELINES") == "1";

    private static void AssertLooksLike(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("nothing was rendered");
        var baseline = Path.Combine(BaselineDirectory, name + ".png");

        if (Updating || !File.Exists(baseline))
        {
            Directory.CreateDirectory(SourceBaselineDirectory);
            frame.Save(Path.Combine(SourceBaselineDirectory, name + ".png"));
            if (!Updating)
            {
                // first run on a machine records the reference rather than failing
                Directory.CreateDirectory(BaselineDirectory);
                frame.Save(baseline);
            }
            return;
        }

        using var expected = new Bitmap(baseline);
        Assert.Equal(expected.PixelSize, frame.PixelSize);

        var (differing, total) = Compare(expected, frame);
        var share = total == 0 ? 0 : (double)differing / total;
        if (share > 0.01)
        {
            var actual = Path.Combine(Path.GetTempPath(), name + ".actual.png");
            frame.Save(actual);
            Assert.Fail($"{name} differs from its reference in {share:P2} of pixels; the frame was written to {actual}");
        }
    }

    /// <summary>
    /// Compares the two frames after averaging each three-by-three block into one value. A glyph
    /// hinted a shade differently disappears into its block; a control that moved, changed width or
    /// stopped being drawn does not.
    /// </summary>
    private static (int Differing, int Total) Compare(Bitmap expected, Bitmap actual)
    {
        var size = expected.PixelSize;
        var a = Coarsen(expected, size, out var width, out var height);
        var b = Coarsen(actual, size, out _, out _);
        var differing = 0;
        for (var i = 0; i < a.Length; i += 3)
        {
            var dr = Math.Abs(a[i] - b[i]);
            var dg = Math.Abs(a[i + 1] - b[i + 1]);
            var db = Math.Abs(a[i + 2] - b[i + 2]);
            if (dr + dg + db > 48) differing++;
        }
        return (differing, width * height);
    }

    private const int Block = 3;

    /// <summary>Averages each block of pixels into one red, green and blue triple.</summary>
    private static double[] Coarsen(Bitmap bitmap, PixelSize size, out int width, out int height)
    {
        var count = size.Width * size.Height;
        var raw = new byte[count * 4];
        unsafe
        {
            fixed (byte* p = raw) bitmap.CopyPixels(new PixelRect(size), (IntPtr)p, raw.Length, size.Width * 4);
        }
        width = (size.Width + Block - 1) / Block;
        height = (size.Height + Block - 1) / Block;
        var sums = new double[width * height * 3];
        var counts = new int[width * height];
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var source = (y * size.Width + x) * 4;
                var cell = (y / Block) * width + (x / Block);
                sums[cell * 3] += raw[source];
                sums[cell * 3 + 1] += raw[source + 1];
                sums[cell * 3 + 2] += raw[source + 2];
                counts[cell]++;
            }
        }
        for (var cell = 0; cell < counts.Length; cell++)
        {
            if (counts[cell] == 0) continue;
            sums[cell * 3] /= counts[cell];
            sums[cell * 3 + 1] /= counts[cell];
            sums[cell * 3 + 2] /= counts[cell];
        }
        return sums;
    }

    [AvaloniaFact]
    public void The_review_workspace_looks_like_its_reference()
    {
        var (vm, _, folder) = ReviewWorkspaceTests.NewAnalyticsForVisuals();
        var window = new Window
        {
            Content = new AnalyticsView { DataContext = vm },
            Width = 1600,
            Height = 900,
        };
        window.Show();
        AssertLooksLike(window, "review-workspace");
        window.Close();
        try { Directory.Delete(folder, true); } catch { }
    }

    [AvaloniaFact]
    public void The_ion_table_window_looks_like_its_reference()
    {
        var (vm, _, folder) = ReviewWorkspaceTests.NewAnalyticsForVisuals();
        vm.IonTableDetached = true;
        var window = new IonTableWindow { DataContext = vm, Width = 1180, Height = 700 };
        window.Show();
        AssertLooksLike(window, "ion-table-window");
        window.Close();
        try { Directory.Delete(folder, true); } catch { }
    }

    [AvaloniaFact]
    public void The_export_dialog_looks_like_its_reference()
    {
        var chart = new OpenDIAL.Desktop.Controls.SpectrumChart
        {
            Peaks = new[]
            {
                new Point(70.07, 22), new Point(88.08, 58), new Point(106.09, 100),
                new Point(227.20, 41), new Point(288.26, 96),
            },
            ReferencePeaks = new[] { new Point(88.08, 30), new Point(182.19, 100), new Point(288.25, 24) },
            PrecursorMz = 288.2543,
            Title = "Representative MS/MS · Cer d12:0/4:0 (spot 196, m/z 288.2543) · mirror: library reference",
            YLabel = "Relative intensity (%)",
            XLabel = "m/z",
        };
        var behind = new Window { Content = chart, Width = 620, Height = 300 };
        behind.Show();
        behind.UpdateLayout();

        var window = new ChartExportWindow { Width = 820, Height = 560 };
        window.Show();
        window.Prepare(chart, "Representative MS/MS", new OpenDIAL.Desktop.Charts.ChartExportOptions());
        AssertLooksLike(window, "export-dialog");
        window.Close();
        behind.Close();
    }

    [AvaloniaFact]
    public void The_help_window_looks_like_its_reference()
    {
        var vm = new Help.HelpViewModel(Help.Manual.Load());
        vm.Open("review-tags");
        var window = new HelpWindow { DataContext = vm, Width = 1180, Height = 820 };
        window.Show();
        AssertLooksLike(window, "help-window");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_drift_correction_looks_like_its_reference()
    {
        var (vm, _) = StatisticsWorkspaceTests.Drifting();
        await vm.ApplyCorrectionCommand.ExecuteAsync(null);
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        StatisticsWorkspaceTests.SelectPage(tabs, "Drift correction");   // its table and its two traces
        AssertLooksLike(window, "statistics-drift");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_discriminant_model_looks_like_its_reference()
    {
        var (vm, _) = StatisticsWorkspaceTests.Comparison();   // two classes with a real difference
        vm.PlsPermutations = "200";
        await vm.FitDiscriminantCommand.ExecuteAsync(null);
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        StatisticsWorkspaceTests.SelectPage(tabs, "Discriminant");
        AssertLooksLike(window, "statistics-discriminant");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_orthogonal_model_looks_like_its_reference()
    {
        var (vm, _) = StatisticsWorkspaceTests.Comparison();
        vm.OplsPermutations = "200";
        await vm.FitOrthogonalCommand.ExecuteAsync(null);
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        StatisticsWorkspaceTests.SelectPage(tabs, "Orthogonal");
        AssertLooksLike(window, "statistics-orthogonal");
        window.Close();
    }

    /// <summary>The one-factor pages on a reviewed result: the standards chosen, the comparison run.</summary>
    private static (Window Window, TabControl Tabs) OneFactorWindow()
    {
        var (vm, _, _) = OneFactorTests.Reviewed();
        vm.Analysis.ClassA = "treated";
        vm.Analysis.ClassB = "control";
        vm.Analysis.CompareCommand.Execute(null);
        vm.Analysis.HeatmapTopText = "14";
        vm.Analysis.BuildHeatmapCommand.Execute(null);
        vm.Analysis.MinimumSetSizeText = "2";
        vm.Analysis.RunEnrichmentCommand.Execute(null);
        vm.Analysis.SelectedComparison = vm.Analysis.ComparisonRows.OrderBy(r => r.P).First();
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();
        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        return (window, tabs);
    }

    [AvaloniaFact]
    public void The_data_page_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Data processing");
        AssertLooksLike(window, "statistics-data");
        window.Close();
    }

    [AvaloniaFact]
    public void The_normalisation_check_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Normalisation check");
        AssertLooksLike(window, "statistics-normalisation");
        window.Close();
    }

    [AvaloniaFact]
    public void The_volcano_plot_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Volcano plot");
        AssertLooksLike(window, "statistics-volcano");
        window.Close();
    }

    [AvaloniaFact]
    public void The_statistical_test_page_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Statistical test");
        AssertLooksLike(window, "statistics-test");
        window.Close();
    }

    [AvaloniaFact]
    public void The_heatmap_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Heatmap");
        AssertLooksLike(window, "statistics-heatmap");
        window.Close();
    }

    [AvaloniaFact]
    public void The_enrichment_page_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Lipid enrichment");
        AssertLooksLike(window, "statistics-enrichment");
        window.Close();
    }

    [AvaloniaFact]
    public void The_principal_components_look_like_their_reference()
    {
        var (window, tabs) = OneFactorWindow();
        StatisticsWorkspaceTests.SelectPage(tabs, "Principal components");
        AssertLooksLike(window, "statistics-pca");
        window.Close();
    }

    [AvaloniaFact]
    public void The_pathways_page_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        var vm = (StatisticsViewModel)((StatisticsView)window.Content!).DataContext!;
        vm.Pathways.ComputeCommand.Execute(null);
        StatisticsWorkspaceTests.SelectPage(tabs, "Pathways");
        AssertLooksLike(window, "statistics-pathways");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_two_factor_page_looks_like_its_reference()
    {
        var (window, tabs) = OneFactorWindow();
        var vm = (StatisticsViewModel)((StatisticsView)window.Content!).DataContext!;
        vm.TwoFactor.PermutationsText = "99";
        await vm.TwoFactor.ComputeCommand.ExecuteAsync(null);
        StatisticsWorkspaceTests.SelectPage(tabs, "Two factors");
        AssertLooksLike(window, "statistics-two-factor");
        window.Close();
    }

    [AvaloniaFact]
    public void The_standards_dialog_looks_like_its_reference()
    {
        var (_, _, spots) = OneFactorTests.Reviewed();
        var confirmed = spots.Take(16).ToList();
        var window = new StandardsWindow(confirmed, RelativeAbundance.Suggest(confirmed));
        window.Show();
        AssertLooksLike(window, "standards-dialog");
        window.Close();
    }
}
