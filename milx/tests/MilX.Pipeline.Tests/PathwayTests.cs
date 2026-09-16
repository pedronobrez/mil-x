using MilX.Pipeline.Results;
using MilX.Pipeline.Statistics;
using MilX.Pipeline.Statistics.Pathways;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>The BioPAN-style pathway analysis: the network, the weights, the Z-scores and the chains.</summary>
public class PathwayTests
{
    [Fact]
    public void The_reaction_table_reads_and_names_classes_the_parser_knows()
    {
        var reactions = ReactionDatabase.Reactions;
        // BioPAN's database: 51 between classes, 13 over the ethers, 3 over the dihydro bases, 30 between fatty acids
        Assert.Equal(97, reactions.Count(r => r.IsBioPan));
        Assert.Equal(30, reactions.Count(r => r.IsBioPan && r.Kind == ReactionKind.FattyAcid));
        Assert.True(reactions.Count(r => !r.IsBioPan) >= 10, "the extensions are marked as such");
        Assert.Contains(reactions, r => r.Id == "PEMT" && r.Reactant == "PE" && r.Product == "PC" && r.Genes.SequenceEqual(new[] { "PEMT" }));
        // the genes as BioPAN names them
        Assert.Equal(new[] { "PLPP1", "PLPP2", "PLPP3" }, reactions.First(r => r.Reactant == "PA" && r.Product == "DG").Genes);
        Assert.Equal(11, reactions.First(r => r.Reactant == "PC" && r.Product == "LPC").Genes.Count);
        Assert.Equal(new[] { "PLA2G4C" }, reactions.First(r => r.Reactant == "PE" && r.Product == "LPE").Genes);
        Assert.Contains(reactions, r => r.Reactant == "PA" && r.Product == "PS" && r.Genes.Contains("PTDSS1"));
        Assert.Contains(reactions, r => r.Reactant == "PC" && r.Product == "CL" && r.Genes.Contains("TAZ"));
        Assert.Contains(reactions, r => r.Reactant == "FA 22:5" && r.Product == "FA 24:5" && r.Genes.SequenceEqual(new[] { "ELOVL2" }));
        // human symbols throughout: BioPAN's mouse Scd1 and Scd3 are SCD and SCD5 here
        Assert.Equal(new[] { "SCD" }, reactions.First(r => r.Reactant == "FA 18:0" && r.Product == "FA 18:1").Genes);
        Assert.DoesNotContain(reactions, r => r.Genes.Any(g => g != g.ToUpperInvariant() || g is "SCD1" or "SCD3"));
        Assert.Contains(reactions, r => r.Reactant == "O-PE" && r.Product == "P-PE" && r.Genes.Contains("PEDS1"));
        Assert.Contains(reactions, r => r.Reactant == "PC" && r.Product == "LPC" && r.Kind == ReactionKind.RemovesChain);
        Assert.Contains(reactions, r => r.Reactant == "Chol" && r.Product == "CE" && r.Kind == ReactionKind.ClassOnly && !r.IsBioPan);
        Assert.Equal(reactions.Count, reactions.Select(r => r.Id).Distinct().Count());
        Assert.All(reactions, r => Assert.NotEmpty(r.Genes));
        // every class a reaction names is one the chain-count rule knows to be one- or two-chained
        Assert.All(ReactionDatabase.Classes, c => Assert.True(LipidNames.ChainCount(c) is 1 or 2 or 3 or 4, c));
    }

    [Theory]
    [InlineData("Cer_NS", "Cer 18:1;O2/16:0", "Cer")]
    [InlineData("Cer_NDS", "Cer 18:0;O2/16:0", "dhCer")]
    [InlineData("Cer_AS", "Cer 18:1;O2/24:0;O", "Cer")]
    [InlineData("SM", "SM 34:1;O2", "SM")]
    [InlineData("SM", "SM 34:0;O2", "dhSM")]
    [InlineData("Sph", "Sph 18:1;O2", "SPB")]
    [InlineData("DHSph", "Sph 18:0;O2", "dhSPB")]
    [InlineData("HexCer_AP", "HexCer 42:1;O3", "HexCer")]
    [InlineData("EtherPC", "PC O-34:1", "O-PC")]
    [InlineData("EtherPC", "PC P-34:1", "P-PC")]
    [InlineData("EtherPE", "PE P-16:0_20:4", "P-PE")]
    [InlineData("EtherLPE", "LPE O-16:0", "O-LPE")]
    [InlineData("Hex2Cer", "Hex2Cer 34:1;O2", "LacCer")]
    [InlineData("PC_d5", "PC 33:1 d5", "PC")]
    [InlineData("CerP", "CerP 34:1;O2", "Cer1P")]
    [InlineData("PC", "PC 34:1", "PC")]
    [InlineData("Unknown", "Unknown", null)]
    public void Ontologies_fold_into_the_network_classes(string ontology, string name, string? expected) =>
        Assert.Equal(expected, ReactionDatabase.NetworkClass(ontology, name));

    [Theory]
    [InlineData("PC 34:1", new[] { "34:1" })]
    [InlineData("PC 16:0_18:1", new[] { "16:0", "18:1" })]
    [InlineData("TG 52:2|TG 16:0_18:1_18:1", new[] { "16:0", "18:1", "18:1" })]
    [InlineData("Cer 18:1;O2/16:0", new[] { "18:1", "16:0" })]
    [InlineData("SM d18:1/16:0", new[] { "18:1", "16:0" })]
    [InlineData("PC O-16:0_18:1", new[] { "16:0", "18:1" })]
    [InlineData("LPC 16:0", new[] { "16:0" })]
    public void Chains_are_read_off_the_molecular_species(string name, string[] chains) =>
        Assert.Equal(chains, LipidNames.Chains(name));

    /// <summary>
    /// Six injections, three per class, with PE, PC, LPC and DG. In the treated class PC is three
    /// times higher against a PE that does not move, so PE → PC is active; LPC does not move, so
    /// PC → LPC is suppressed by the same amount; DG → PC is active too and PE → PC → LPC cancels.
    /// </summary>
    private static AnalysisTable Table()
    {
        var samples = new List<SampleInfo>();
        for (var i = 0; i < 6; i++) samples.Add(new SampleInfo(i, (i < 3 ? "T" : "C") + (i % 3 + 1), i < 3 ? "treated" : "control", "Sample"));
        var rows = new[]
        {
            (Name: "PE 34:1|PE 16:0_18:1", Ontology: "PE", Base: 1000.0, Fold: 1.0),
            (Name: "PE 36:2|PE 18:1_18:1", Ontology: "PE", Base: 800.0, Fold: 1.0),
            (Name: "PC 34:1|PC 16:0_18:1", Ontology: "PC", Base: 5000.0, Fold: 3.0),
            (Name: "PC 36:2|PC 18:1_18:1", Ontology: "PC", Base: 4000.0, Fold: 3.0),
            (Name: "LPC 16:0", Ontology: "LPC", Base: 300.0, Fold: 1.0),
            (Name: "LPC 18:1", Ontology: "LPC", Base: 250.0, Fold: 1.0),
            (Name: "DG 34:1|DG 16:0_18:1", Ontology: "DG", Base: 600.0, Fold: 1.0),
            // the free fatty acids and the acyl-CoA BioPAN's species rule looks for
            (Name: "FA 16:0", Ontology: "FA", Base: 100.0, Fold: 1.0),
            (Name: "FA 18:1", Ontology: "FA", Base: 120.0, Fold: 1.0),
            (Name: "FACoA 18:1", Ontology: "FACoA", Base: 10.0, Fold: 1.0),
        };
        var rng = new Random(5);
        var list = new List<AlignmentSpotRow>();
        var values = new double[6, rows.Length];
        for (var j = 0; j < rows.Length; j++)
        {
            var r = rows[j];
            list.Add(new AlignmentSpotRow { Id = j, Name = r.Name, Ontology = r.Ontology, Mz = 700 + j, Rt = 5 + j * 0.1 });
            for (var i = 0; i < 6; i++) values[i, j] = r.Base * (i < 3 ? r.Fold : 1.0) * (0.95 + rng.NextDouble() * 0.1);
        }
        return new AnalysisTable(values, samples, list, "area");
    }

    [Fact]
    public void Class_level_reactions_score_the_shift_from_pe_to_pc()
    {
        var result = LipidPathways.Compute(Table(), "treated", "control", PathwayLevel.Class, threshold: 1.645, maxPathLength: 3);

        Assert.Equal(5, result.Nodes.Count);   // PE, PC, LPC, DG, FA
        var pemt = Assert.Single(result.Reactions, r => r.Id == "PEMT");
        Assert.Equal("active", pemt.Status);
        Assert.InRange(pemt.Log2Change, 1.4, 1.75);          // log2 3 = 1.585, less the noise
        Assert.True(pemt.Z > 1.645, pemt.Z.ToString());
        Assert.Contains("PEMT", pemt.Genes);

        var pla2 = Assert.Single(result.Reactions, r => r.Id == "PLA2-PC");
        Assert.Equal("suppressed", pla2.Status);
        Assert.InRange(pla2.Log2Change, -1.75, -1.4);
        Assert.True(pla2.Z < -1.645 && Math.Abs(pla2.Z + pemt.Z) < 1.5, $"{pemt.Z} vs {pla2.Z}");   // the same shift seen from the other side

        Assert.Equal("active", Assert.Single(result.Reactions, r => r.Id == "LPCAT").Status);
        Assert.Equal("active", Assert.Single(result.Reactions, r => r.Id == "CPT").Status);
        // the extensions stay out unless asked for, and come in marked when they are
        Assert.DoesNotContain(result.Predicted, p => p.Reaction == "PE → PA");   // PLD on PE is MIL-X's addition
        var extended = LipidPathways.Compute(Table(), "treated", "control", includeExtensions: true);
        Assert.Contains(extended.Predicted, p => p.Reaction == "PE → PA" && p.Missing == "PA");

        // the chain PE → PC → LPC cancels; PE → PC alone stands
        var chain = result.Pathways.First(p => p.Chain == "PE → PC → LPC");
        Assert.Equal("unchanged", chain.Status);
        Assert.InRange(chain.Z, -1, 1);
        var single = result.Pathways.First(p => p.Chain == "PE → PC");
        Assert.Equal(pemt.Z, single.Z, 9);
        Assert.DoesNotContain(result.Pathways, p => p.Chain.Contains("PS"));   // PS is not measured, so no such chain

        // and what one more confirmed class would open up
        Assert.Contains(result.Predicted, p => p.Reaction == "PC → PS" && p.Missing == "PS");
        Assert.Contains("active", result.Message);
    }

    [Fact]
    public void Species_level_reactions_follow_the_chains()
    {
        var result = LipidPathways.Compute(Table(), "treated", "control", PathwayLevel.Species);

        Assert.Contains(result.Reactions, r => r.Reactant == "PE 34:1" && r.Product == "PC 34:1" && r.Status == "active");
        Assert.Contains(result.Reactions, r => r.Reactant == "PE 36:2" && r.Product == "PC 36:2" && r.Status == "active");
        // BioPAN's rule: PC 34:1 → LPC 16:0 releases 18:1 and PC 34:1 → LPC 18:1 releases 16:0, both measured as free acids
        Assert.Contains(result.Reactions, r => r.Reactant == "PC 34:1" && r.Product == "LPC 16:0" && r.Id == "PLA2-PC");
        Assert.Contains(result.Reactions, r => r.Reactant == "PC 34:1" && r.Product == "LPC 18:1");
        // PC 36:2 → LPC 16:0 would release 20:2, which is not measured; → LPC 18:1 releases 18:1, which is
        Assert.DoesNotContain(result.Reactions, r => r.Reactant == "PC 36:2" && r.Product == "LPC 16:0");
        Assert.Contains(result.Reactions, r => r.Reactant == "PC 36:2" && r.Product == "LPC 18:1");
        // adding a chain needs the acyl-CoA: 18:1-CoA is measured, 16:0-CoA is not
        Assert.Contains(result.Reactions, r => r.Reactant == "LPC 18:1" && r.Product == "PC 36:2" && r.Id == "LPCAT");
        Assert.Contains(result.Reactions, r => r.Reactant == "LPC 16:0" && r.Product == "PC 34:1");
        Assert.DoesNotContain(result.Reactions, r => r.Reactant == "LPC 18:1" && r.Product == "PC 34:1");
        Assert.Contains(result.Reactions, r => r.Reactant == "DG 34:1" && r.Product == "PC 34:1");
        Assert.DoesNotContain(result.Reactions, r => r.Reactant == "DG 34:1" && r.Product == "PC 36:2");
        Assert.Empty(result.Predicted);
    }

    [Fact]
    public void Fatty_acid_level_is_the_free_fatty_acids_measured()
    {
        var result = LipidPathways.Compute(Table(), "treated", "control", PathwayLevel.FattyAcid);

        // BioPAN's fatty-acid graph is the free fatty acids measured: FA 16:0 and FA 18:1, not the chains of the phospholipids
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Nodes.First(n => n.Name == "FA 16:0").Members);
        Assert.Equal(1, result.Nodes.First(n => n.Name == "FA 18:1").Members);
        // BioPAN's table has 16:0 → 18:0 and 16:0 → 16:1, but neither 18:0 nor 16:1 is measured; 18:1 → 18:2, 18:1 → 20:1 likewise
        Assert.Empty(result.Reactions);
        Assert.Contains("No reaction", result.Message);

        // and without any free fatty acid the page says so rather than showing an empty graph
        var none = LipidPathways.Compute(Table().Select(Enumerable.Range(0, 7).ToList()), "treated", "control", PathwayLevel.FattyAcid);
        Assert.Empty(none.Nodes);
        Assert.Contains("No free fatty acid", none.Message);
    }

    [Fact]
    public void Too_few_replicates_is_said_rather_than_scored()
    {
        var table = Table().SelectSamples(new[] { 0, 3 });
        var result = LipidPathways.Compute(table, "treated", "control");
        Assert.Empty(result.Reactions);
        Assert.Contains("at least 2", result.Message);
    }

    [Fact]
    public void The_signed_z_is_the_normal_quantile_of_the_two_sided_p()
    {
        Assert.Equal(1.96, LipidPathways.SignedZ(0.05, +1), 2);
        Assert.Equal(-1.96, LipidPathways.SignedZ(0.05, -1), 2);
        Assert.Equal(1.645, LipidPathways.SignedZ(0.10, +1), 2);
        Assert.Equal(0, LipidPathways.SignedZ(1, +1), 6);
        Assert.True(LipidPathways.SignedZ(0, +1) < 8.1 && LipidPathways.SignedZ(0, +1) > 7);
    }

    [Fact]
    public void The_paired_comparison_pairs_the_injections_in_order()
    {
        var paired = LipidPathways.Compute(Table(), "treated", "control", paired: true);
        var pemt = Assert.Single(paired.Reactions, r => r.Id == "PEMT");
        Assert.Equal("active", pemt.Status);
        Assert.Contains("paired", paired.Message);
        // the p is the paired t-test's on the weights, pair by pair, and not Welch's
        var (_, expected) = Univariate.PairedT(pemt.WeightsA.ToArray(), pemt.WeightsB.ToArray());
        Assert.Equal(expected, pemt.P, 12);
        var unpaired = LipidPathways.Compute(Table(), "treated", "control").Reactions.First(r => r.Id == "PEMT");
        Assert.NotEqual(unpaired.P, pemt.P);

        // pairing needs the same number of injections in each class
        var uneven = LipidPathways.Compute(Table().SelectSamples(new[] { 0, 1, 2, 3, 4 }), "treated", "control", paired: true);
        Assert.Empty(uneven.Reactions);
        Assert.Contains("same number", uneven.Message);
    }
}
