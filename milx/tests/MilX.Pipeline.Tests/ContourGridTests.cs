using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

public class ContourGridTests
{
    private static RawMeasurement Run(int scans, double mzLo = 100, double mzHi = 200)
    {
        var list = new List<RawSpectrum>();
        for (var i = 0; i < scans; i++)
        {
            // one peak walking up the mass axis in odd bins, one ridge at a fixed mass (bin 50 of 100), intensity rising with time
            var peaks = new[]
            {
                new RawPeakElement { Mz = 150.0, Intensity = 10 },
                new RawPeakElement { Mz = mzLo + 1.3 + 2 * (i % 49), Intensity = 100 + i },
            }.OrderBy(p => p.Mz).ToArray();
            list.Add(new RawSpectrum { Index = i, MsLevel = 1, ScanStartTime = i * 0.5, ScanStartTimeUnit = Units.Second, ScanWindowLowerLimit = mzLo, ScanWindowUpperLimit = mzHi, Spectrum = peaks });
        }
        return new RawMeasurement { SpectrumList = list };
    }

    [Fact]
    public void Every_scan_is_a_row_under_the_limit_and_the_ridge_is_a_column()
    {
        var raw = Run(50);
        var grid = ContourGrid.Build(raw, Enumerable.Range(0, 50).ToList(), bins: 100);
        Assert.Equal(50, grid.Rows);
        Assert.Equal(100, grid.Bins);
        Assert.Equal(1, grid.Grouped);
        Assert.Equal(string.Empty, grid.Note);
        Assert.Equal((100.0, 200.0), grid.MzRange);
        // the ridge at 150 sits in bin 50 of every row
        var column = grid.BinOf(150.0);
        Assert.Equal(50, column);
        for (var r = 0; r < grid.Rows; r++) Assert.Equal(10, grid.Intensity[r, column]);
        Assert.Equal(10, grid.At(0.2, 150.0));
        // times are minutes
        Assert.Equal(0.0, grid.Rt[0]);
        Assert.Equal(49 * 0.5 / 60.0, grid.Rt[^1], 9);
    }

    [Fact]
    public void Scans_over_the_row_limit_are_averaged_not_skipped()
    {
        var raw = Run(100);
        var grid = ContourGrid.Build(raw, Enumerable.Range(0, 100).ToList(), bins: 50, maxRows: 40);
        Assert.Equal(3, grid.Grouped);
        Assert.Equal(34, grid.Rows);
        Assert.Equal("3 scans averaged into each row", grid.Note);
        // the ridge keeps its height in an averaged row, and in the short last row too
        var column = grid.BinOf(150.0);
        Assert.Equal(10, grid.Intensity[0, column]);
        Assert.Equal(10, grid.Intensity[grid.Rows - 1, column]);
        Assert.Equal(100, grid.Scans);
    }

    [Fact]
    public void Slices_and_the_percentile_read_the_grid()
    {
        var raw = Run(20);
        var grid = ContourGrid.Build(raw, Enumerable.Range(0, 20).ToList(), bins: 100);
        var xic = grid.SliceRt(149, 151);
        Assert.Equal(20, xic.Count);
        Assert.All(xic, p => Assert.Equal(10, p.Intensity));
        var spectrum = grid.SliceMz(0, 1);
        Assert.Equal(100, spectrum.Count);
        Assert.Equal(10, spectrum[50].Intensity);
        Assert.True(grid.Percentile(99.7) >= 100);
        Assert.Equal(0, ContourGrid.Empty("x").Percentile());
    }

    [Fact]
    public void Ranges_restrict_the_grid_and_an_edge_value_goes_right()
    {
        var raw = Run(20);
        var grid = ContourGrid.Build(raw, Enumerable.Range(0, 20).ToList(), rtRange: (0.05, 0.10), mzRange: (140, 160), bins: 40);
        Assert.True(grid.Rows is >= 6 and <= 8);
        Assert.Equal((140.0, 160.0), grid.MzRange);
        Assert.Equal(20, grid.BinOf(150.0));      // exactly on an edge: the bin to its right
        Assert.Equal(39, grid.BinOf(160.0));      // the top edge belongs to the last bin
        Assert.Equal(-1, grid.BinOf(99));
        Assert.Equal("no scans between 5.00 and 6.00 min", ContourGrid.Build(raw, Enumerable.Range(0, 20).ToList(), rtRange: (5, 6)).Note);
    }
}
