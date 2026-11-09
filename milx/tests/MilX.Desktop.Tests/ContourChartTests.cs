using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Desktop.Charts;
using MilX.Desktop.Controls;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Desktop.Tests;

public class ContourChartTests
{
    private static ContourGrid Grid()
    {
        var list = new List<RawSpectrum>();
        for (var i = 0; i < 40; i++)
        {
            list.Add(new RawSpectrum
            {
                Index = i, MsLevel = 1, ScanStartTime = i * 0.01, ScanStartTimeUnit = Units.Minute, ScanWindowLowerLimit = 100, ScanWindowUpperLimit = 300,
                // forty peaks a scan, so the percentile has something to stand on, and one spike in one scan
                Spectrum = Enumerable.Range(0, 40).Select(j => new RawPeakElement { Mz = 110 + j * 4.5, Intensity = j == 31 && i == 20 ? 100000 : 50 + i + j }).ToArray(),
            });
        }
        return ContourGrid.Build(new RawMeasurement { SpectrumList = list }, Enumerable.Range(0, 40).ToList(), bins: 200);
    }

    [AvaloniaFact]
    public void The_surface_renders_to_the_screen_and_to_svg_with_a_percentile_top()
    {
        var chart = new ContourChart { Grid = Grid(), Width = 600, Height = 300 };
        var window = new Window { Content = chart, Width = 600, Height = 300 };
        window.Show();
        window.UpdateLayout();
        using var bitmap = ChartExport.RenderBitmap(chart, 1);
        Assert.True(bitmap.PixelSize.Width >= 600);
        // the spike of one scan does not set the scale: the top is the 99.7th percentile, under 100000
        Assert.True(chart.ScaleTop < 100000, chart.ScaleTop.ToString());
        Assert.True(chart.ScaleTop >= 50);

        var path = Path.Combine(Path.GetTempPath(), "milx-contour-" + Guid.NewGuid().ToString("N") + ".svg");
        try
        {
            ChartExport.Save(chart, path, new ChartExportOptions { Format = "svg" });
            var svg = File.ReadAllText(path);
            Assert.Contains("<image ", svg);
            Assert.Contains("data:image/png;base64,", svg);
            Assert.Contains("Retention time (min)", svg);
        }
        finally { File.Delete(path); }
        window.Close();
    }

    [Fact]
    public void The_intensity_laws_and_the_ramps_behave()
    {
        Assert.Equal(0, ContourChart.Scaled(0, 100, "sqrt"));
        Assert.Equal(0.5, ContourChart.Scaled(25, 100, "sqrt"));
        Assert.Equal(1, ContourChart.Scaled(500, 100, "linear"));   // clipped at the top
        Assert.Equal(0.25, ContourChart.Scaled(25, 100, "linear"));
        Assert.Equal(1, ContourChart.Scaled(100, 100, "log"), 9);
        Assert.True(ContourChart.Scaled(1, 100, "log") > ContourChart.Scaled(1, 100, "linear"));
        Assert.Equal(Color.Parse("#000004"), ContourChart.Sample("inferno", 0));
        Assert.Equal(Color.Parse("#fcffa4"), ContourChart.Sample("inferno", 1));
        Assert.Equal(Color.Parse("#fde725"), ContourChart.Sample("viridis", 1));
        Assert.Equal(Color.Parse("#ffffff"), ContourChart.Sample("greys", 0));
        Assert.Equal(ContourChart.Sample("inferno", 0.5), ContourChart.Sample("no such ramp", 0.5));
    }
}
