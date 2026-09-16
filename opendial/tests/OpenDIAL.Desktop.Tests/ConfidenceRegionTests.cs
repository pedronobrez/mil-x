using OpenDIAL.Desktop.Controls;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The 95 % region drawn around a class on a score plot. The radii are checked against the values
/// R gives for the same quantiles, because the point of the feature is that a figure made here
/// matches one a reader has seen from MetaboAnalyst.
/// </summary>
public sealed class ConfidenceRegionTests
{
    [Fact]
    public void Chi_square_radius_is_the_one_metaboanalyst_draws_by_default()
    {
        // sqrt(qchisq(0.95, 2)) = sqrt(5.991465) = 2.447747
        Assert.Equal(2.447747, ScatterChart.EllipseRadius(EllipseMethod.ChiSquare, 0.95, 8), 5);
    }

    [Fact]
    public void Chi_square_radius_does_not_care_how_big_the_class_is()
    {
        var small = ScatterChart.EllipseRadius(EllipseMethod.ChiSquare, 0.95, 4);
        var large = ScatterChart.EllipseRadius(EllipseMethod.ChiSquare, 0.95, 400);
        Assert.Equal(small, large, 12);
    }

    [Theory]
    // sqrt(2 * qf(0.95, 2, n - 1)), worked out from the closed form of an F with two numerator
    // degrees of freedom: (m/2)((1 - level)^(-2/m) - 1), m = n - 1
    [InlineData(4, 4.370834)]
    [InlineData(6, 3.401804)]
    [InlineData(10, 2.917703)]
    [InlineData(30, 2.579789)]
    public void F_radius_widens_as_the_class_gets_smaller(int n, double expected)
    {
        Assert.Equal(expected, ScatterChart.EllipseRadius(EllipseMethod.F, 0.95, n), 5);
    }

    [Fact]
    public void The_two_definitions_meet_as_the_class_grows()
    {
        var chi = ScatterChart.EllipseRadius(EllipseMethod.ChiSquare, 0.95, 5000);
        var f = ScatterChart.EllipseRadius(EllipseMethod.F, 0.95, 5000);
        Assert.True(f > chi, "the F region is never the smaller one");
        Assert.True(f - chi < 0.01, $"they should have all but met by n = 5000, but differ by {f - chi:0.####}");
    }

    [Fact]
    public void A_round_cloud_gives_a_round_region_of_the_right_radius()
    {
        // a cross of four points one unit from the centre: covariance 2/3 on both axes, no tilt
        var pts = new List<(double X, double Y)> { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var outline = ScatterChart.ConfidenceEllipse(pts, EllipseMethod.ChiSquare);

        Assert.NotEmpty(outline);
        var expected = ScatterChart.EllipseRadius(EllipseMethod.ChiSquare, 0.95, 4) * Math.Sqrt(2.0 / 3.0);
        foreach (var (x, y) in outline)
        {
            Assert.Equal(expected, Math.Sqrt(x * x + y * y), 6);   // centred on the origin, and round
        }
    }

    [Fact]
    public void A_tilted_cloud_tilts_with_it()
    {
        var pts = new List<(double X, double Y)> { (-2, -2), (-1, -1), (1, 1), (2, 2), (0, 0.1) };
        var outline = ScatterChart.ConfidenceEllipse(pts, EllipseMethod.ChiSquare);

        // the long axis runs with the cloud, so the furthest point from the centre is near y = x
        var far = outline.OrderByDescending(p => p.X * p.X + p.Y * p.Y).First();
        Assert.True(Math.Abs(far.X - far.Y) < Math.Abs(far.X) * 0.3, $"the region did not follow the cloud: {far}");
    }

    [Fact]
    public void Two_injections_are_not_enough_for_a_region()
    {
        var pts = new List<(double X, double Y)> { (0, 0), (1, 1) };
        Assert.Empty(ScatterChart.ConfidenceEllipse(pts, EllipseMethod.ChiSquare));
    }
}
