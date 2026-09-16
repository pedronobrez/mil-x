using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// One compound is several features: the protonated molecule, its sodium and ammonium adducts, its
/// isotopes, what fell apart in the source. They elute together and rise and fall together, and the
/// distance between their masses is one an adduct pair gives.
/// </summary>
public class IonIdentityTests
{
    private static IonCandidate Ion(int id, double rt, double mz, double height, double[] profile, string adduct = "[M+H]+", int isotope = 0) =>
        new(id, rt, mz, height, profile, adduct, isotope);

    private static readonly double[] Rising = { 100, 210, 320, 480, 600 };
    private static readonly double[] RisingHalf = { 50, 105, 160, 240, 300 };
    private static readonly double[] Falling = { 600, 480, 320, 210, 100 };

    [Fact]
    public void The_sodium_adduct_joins_the_protonated_molecule()
    {
        var features = new[]
        {
            Ion(1, 5.00, 760.5851, 10000, Rising),
            Ion(2, 5.01, 782.5670, 4000, RisingHalf, "[M+Na]+"),   // +21.9819
        };
        var groups = IonIdentity.Group(features, positive: true);
        Assert.Equal(1, groups[0].RepresentativeId);
        Assert.Equal(1, groups[1].RepresentativeId);
        Assert.Equal("adduct", groups[1].Relation);
        Assert.Contains("[M+Na]+", groups[1].Explanation, StringComparison.Ordinal);
        Assert.True(groups[0].IsRepresentative);
        Assert.False(groups[1].IsRepresentative);
    }

    [Fact]
    public void An_ammonium_adduct_and_an_isotope_and_a_water_loss_all_join()
    {
        var features = new[]
        {
            Ion(1, 3.00, 600.5000, 9000, Rising),
            Ion(2, 3.01, 617.5265, 3000, RisingHalf, "[M+NH4]+"),      // +17.0265
            Ion(3, 3.00, 601.5034, 1800, RisingHalf, "[M+H]+", 1),     // one neutron up
            Ion(4, 2.99, 582.4894, 900, RisingHalf),                   // less water
        };
        var groups = IonIdentity.Group(features, positive: true).ToDictionary(g => g.Id);
        Assert.Equal("adduct", groups[2].Relation);
        Assert.Equal("isotope", groups[3].Relation);
        Assert.Equal("in-source", groups[4].Relation);
        Assert.All(new[] { 2, 3, 4 }, id => Assert.Equal(1, groups[id].RepresentativeId));
    }

    [Fact]
    public void Heights_that_do_not_agree_are_left_apart()
    {
        // the right mass distance, the same retention time, and profiles that contradict each other
        var features = new[]
        {
            Ion(1, 5.00, 760.5851, 10000, Rising),
            Ion(2, 5.00, 782.5670, 4000, Falling, "[M+Na]+"),
        };
        var groups = IonIdentity.Group(features, positive: true);
        Assert.All(groups, g => Assert.True(g.IsRepresentative));
    }

    [Fact]
    public void A_feature_that_elutes_elsewhere_is_left_apart()
    {
        var features = new[]
        {
            Ion(1, 5.00, 760.5851, 10000, Rising),
            Ion(2, 7.40, 782.5670, 4000, RisingHalf, "[M+Na]+"),
        };
        var groups = IonIdentity.Group(features, positive: true);
        Assert.All(groups, g => Assert.True(g.IsRepresentative));
    }

    [Fact]
    public void The_strongest_ion_represents_the_compound()
    {
        var features = new[]
        {
            Ion(1, 5.00, 760.5851, 2000, RisingHalf),
            Ion(2, 5.00, 782.5670, 18000, Rising, "[M+Na]+"),
        };
        var groups = IonIdentity.Group(features, positive: true).ToDictionary(g => g.Id);
        Assert.Equal(2, groups[1].RepresentativeId);
        Assert.True(groups[2].IsRepresentative);
    }

    [Fact]
    public void Every_feature_comes_back_exactly_once()
    {
        var random = new Random(7);
        var features = Enumerable.Range(1, 400)
            .Select(i => Ion(i, random.NextDouble() * 10, 200 + random.NextDouble() * 800, random.Next(100, 50000),
                Enumerable.Range(0, 5).Select(_ => (double)random.Next(10, 1000)).ToArray()))
            .ToList();
        var groups = IonIdentity.Group(features, positive: true);
        Assert.Equal(features.Count, groups.Count);
        Assert.Equal(features.Select(f => f.Id).OrderBy(i => i), groups.Select(g => g.Id).OrderBy(i => i));
        Assert.All(groups, g => Assert.Contains(g.RepresentativeId, features.Select(f => f.Id)));
    }

    [Fact]
    public void The_negative_mode_has_its_own_pairs()
    {
        var features = new[]
        {
            Ion(1, 4.00, 700.5000, 9000, Rising, "[M-H]-"),
            Ion(2, 4.00, 736.4767, 3000, RisingHalf, "[M+Cl]-"),   // +35.9767
        };
        var groups = IonIdentity.Group(features, positive: false).ToDictionary(g => g.Id);
        Assert.Equal("adduct", groups[2].Relation);
        Assert.Equal(1, groups[2].RepresentativeId);
    }
}
