using MilX.Pipeline.Chemistry;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// MIL-X's mass calculator gives MIL-Q's numbers. The expected values were produced by MIL-Q's
/// chemistry.py (parse_formula, monoisotopic_mass, average_mass, rdbe, Adduct.mz, isotope_pattern
/// with min_abundance 0.001 and max_peaks 12) on a fixed list of formulas, and are held here to a
/// microdalton and to a millionth of relative abundance.
/// </summary>
public class ChemistryParityTests
{
    private const double MassTolerance = 1e-6;
    private const double AbundanceTolerance = 1e-6;

    public static IEnumerable<object[]> Masses => new[]
    {
        new object[] { "C18H34O4", "C18H34O4", 314.24570956878, 314.4608514715215, 2.0 },
        new object[] { "C6H4(NO2)2", "C6H4N2O4", 168.01710661628, 168.10720451710156, 6.0 },
        new object[] { "C18H30D4O4", "C18H30D4O4", 318.2708165519, 318.48549556733843, 2.0 },
        new object[] { "C8H10N4O2", "C8H10N4O2", 194.0803755791, 194.19091740897403, 6.0 },
        new object[] { "C44H84NO8P", "C44H84NO8P", 785.59345528548, 786.1151070213012, 4.0 },
        new object[] { "C27H44O", "C27H44O", 384.33921603068, 384.63866730837555, 6.0 },
        new object[] { "C2H5OH", "C2H6O", 46.04186481202, 46.06852124123035, 0.0 },
        new object[] { "[13C]6H12O6", "H12O6[13C]6", 186.08351712912, 186.1118476193928, 1.0 },
        new object[] { "C5H5N5", "C5H5N5", 135.05449518435, 135.1268993123599, 6.0 },
        new object[] { "C16H32O2", "C16H32O2", 256.24023026544, 256.42468832154697, 1.0 },
    };

    [Theory, MemberData(nameof(Masses))]
    public void Formula_masses_and_rdbe_match_milq(string formula, string hill, double mono, double average, double rdbe)
    {
        var counts = Formula.Parse(formula);
        Assert.Equal(hill, Formula.Format(counts));
        Assert.Equal(mono, Formula.MonoisotopicMass(counts), MassTolerance);
        Assert.Equal(average, Formula.AverageMass(counts), MassTolerance);
        Assert.Equal(rdbe, Formula.Rdbe(counts), 1e-9);
    }

    [Fact]
    public void Adduct_mz_matches_milq_for_every_adduct()
    {
        var mono = Formula.MonoisotopicMass(Formula.Parse("C18H34O4"));
        var expected = new Dictionary<string, double>
        {
            ["[M-H]-"] = 313.238433101901, ["[M+Cl]-"] = 349.215110828689, ["[M+HCOO]-"] = 359.24336352878, ["[M+CH3COO]-"] = 373.25901358878,
            ["[M-2H]2-"] = 156.115578317511, ["[M+H]+"] = 315.252986035659, ["[M+NH4]+"] = 332.27953511878, ["[M+Na]+"] = 337.23493377878,
            ["[M+K]+"] = 353.20886766878, ["[M+2H]2+"] = 158.130131251269,
        };
        Assert.Equal(expected.Count, Adduct.All.Count);
        foreach (var adduct in Adduct.All) Assert.Equal(expected[adduct.Name], adduct.Mz(mono), MassTolerance);
        // the isotope-labelled standard and the doubly charged ion, from MIL-Q's output too
        Assert.Equal(160.142684742829, Adduct.ByName("[M+2H]2+")!.Mz(Formula.MonoisotopicMass(Formula.Parse("C18H30D4O4"))), MassTolerance);
        Assert.Equal(820.562856545389, Adduct.ByName("M+Cl-")!.Mz(Formula.MonoisotopicMass(Formula.Parse("C44H84NO8P"))), MassTolerance);
        Assert.Null(Adduct.ByName("[M+Li]+"));
    }

    public static IEnumerable<object[]> Patterns => new[]
    {
        new object[] { "C18H34O4", new[] { (315.252986035659, 1.0), (316.2564045350008, 0.20011726156820742), (317.2590432579223, 0.02718519365597194), (318.26170986217465, 0.0027683790195250986) } },
        new object[] { "C6H4(NO2)2", new[] { (169.024383083159, 1.0), (170.02715128740815, 0.07418472126926356), (171.028893440727, 0.010605406808260146) } },
        new object[] { "C18H30D4O4", new[] { (319.278093018779, 1.0), (320.2815049321097, 0.19965720866212322), (321.28413820923606, 0.02709450484462645), (322.286801645113, 0.0027563633985541636) } },
        new object[] { "C44H84NO8P", new[] { (786.600731752359, 1.0), (787.6041023702971, 0.4922538591073467), (788.6071698067328, 0.13500490712338406), (789.6100896403688, 0.02670120686131804), (790.6129220937664, 0.004197460812322537) } },
        new object[] { "C27H44O", new[] { (385.346492497559, 1.0), (386.3498981478587, 0.29746617152002447), (387.3531888847034, 0.044714414174101096), (388.35637203872994, 0.0045371879221252865) } },
        new object[] { "[13C]6H12O6", new[] { (187.090793595999, 1.0), (188.09578615123095, 0.0036657126142198), (189.09504058166752, 0.01233427706912956) } },
        new object[] { "C5H5N5", new[] { (136.061771651229, 1.0), (137.06356638749605, 0.07292019761995264), (138.06510547657066, 0.002332695847903974) } },
    };

    [Theory, MemberData(nameof(Patterns))]
    public void The_isotope_pattern_of_the_protonated_ion_matches_milq(string formula, (double Mz, double Abundance)[] expected)
    {
        var pattern = Formula.IsotopePattern(Formula.Parse(formula), Adduct.ByName("[M+H]+"), minAbundance: 0.001, maxPeaks: 12);
        Assert.Equal(expected.Length, pattern.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Mz, pattern[i].Mz, MassTolerance);
            Assert.Equal(expected[i].Abundance, pattern[i].Abundance, AbundanceTolerance);
        }
    }

    [Fact]
    public void Mass_errors_match_milq()
    {
        Assert.Equal(3.3840154561247324, Formula.MassErrorPpm(313.2384, 313.23734), 1e-9);
        Assert.Equal(1.06, Formula.MassErrorMda(313.2384, 313.23734), 1e-9);
    }

    [Fact]
    public void A_bad_formula_is_refused_in_words()
    {
        Assert.Throws<FormulaException>(() => Formula.Parse("C6H4(NO2"));
        Assert.Throws<FormulaException>(() => Formula.Parse("Xx2"));
        Assert.Throws<FormulaException>(() => Formula.Parse("[14C]6"));
        Assert.Throws<FormulaException>(() => Formula.Parse("c6h6"));
        Assert.Empty(Formula.Parse("  "));
    }
}
