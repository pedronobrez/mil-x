using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// Renders a workspace and compares the frame with a stored one.
///
/// The headless tests catch a broken binding but not a broken layout: a panel that collapses to
/// nothing, a strip that overflows, a column that clips its own text all bind perfectly and look
/// wrong. These catch that.
///
/// Text rasterisation differs a little between machines, so the comparison allows a small share of
/// differing pixels rather than demanding an exact match. Set OPENDIAL_UPDATE_BASELINES=1 to write
/// the current frames as the new reference, and look at the result before committing it.
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
        if (share > 0.02)
        {
            var actual = Path.Combine(Path.GetTempPath(), name + ".actual.png");
            frame.Save(actual);
            Assert.Fail($"{name} differs from its reference in {share:P2} of pixels; the frame was written to {actual}");
        }
    }

    /// <summary>Pixels differing by more than a little, so anti-aliasing and hinting do not count.</summary>
    private static (int Differing, int Total) Compare(Bitmap expected, Bitmap actual)
    {
        var size = expected.PixelSize;
        var count = size.Width * size.Height;
        var a = new byte[count * 4];
        var b = new byte[count * 4];
        unsafe
        {
            fixed (byte* pa = a) expected.CopyPixels(new PixelRect(size), (IntPtr)pa, a.Length, size.Width * 4);
            fixed (byte* pb = b) actual.CopyPixels(new PixelRect(size), (IntPtr)pb, b.Length, size.Width * 4);
        }
        var differing = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            var dr = Math.Abs(a[i] - b[i]);
            var dg = Math.Abs(a[i + 1] - b[i + 1]);
            var db = Math.Abs(a[i + 2] - b[i + 2]);
            if (dr + dg + db > 48) differing++;
        }
        return (differing, count);
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
    public async Task The_drift_correction_looks_like_its_reference()
    {
        var (vm, _) = StatisticsWorkspaceTests.Drifting();
        await vm.ApplyCorrectionCommand.ExecuteAsync(null);
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        tabs.SelectedIndex = 1;   // the drift correction, with its table and its two traces
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
        tabs.SelectedIndex = 2;
        AssertLooksLike(window, "statistics-discriminant");
        window.Close();
    }
}
