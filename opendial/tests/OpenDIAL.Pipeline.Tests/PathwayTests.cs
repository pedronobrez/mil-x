using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using OpenDIAL.Pipeline.Statistics.Pathways;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>The BioPAN-style pathway analysis: the network, the weights, the Z-scores and the chains.</summary>
public class PathwayTests
{
    [Fact]
    public void The_reaction_table_reads_and_names_classes_the_parser_knows()
    {
        var reactions = ReactionDatabase.Reactions;
        Assert.True(reactions.Count >= 60, reactions.Count.ToString());
        Assert.Contains(reactions, r => r.Id == "PEMT" && r.Reactant == "PE" && r.Product == "PC" && r.Genes.Contains("PEMT"));
        Assert.Contains(reactions, r => r.Reactant == "PC" && r.Product == "LPC" && r.Kind == ReactionKind.RemovesChain);
        Assert.Contains(reactions, r => r.Reactant == "Chol" && r.Product == "CE" && r.Kind == ReactionKind.ClassOnly);
        Assert.Equal(reactions.Count, reactions.Select(r => r.Id).Distinct().Count());
        Assert.All(reactions, r => Assert.NotEmpty(r.Genes));
    }

    [Theory]
    [InlineData("Cer_NS", "Cer")]
    [InlineData("HexCer_AP", "HexCer")]
    [InlineData("EtherPC", "PC O-")]
    [InlineData("SPB", "Sph")]
    [InlineData("Hex2Cer", "LacCer")]
    [InlineData("PC", "PC")]
    [InlineData("Unknown", null)]
    public void Ontologies_fold_into_the_network_classes(string ontology, string? expected) =>
        Assert.Equal(expected, ReactionDatabase.NetworkClass(ontology));

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

        Assert.Equal(4, result.Nodes.Count);   // PE, PC, LPC, DG
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
        // PC 16:0_18:1 loses either chain; both lyso species are measured
        Assert.Contains(result.Reactions, r => r.Reactant == "PC 34:1" && r.Product == "LPC 16:0" && r.Id == "PLA2-PC");
        Assert.Contains(result.Reactions, r => r.Reactant == "PC 34:1" && r.Product == "LPC 18:1");
        // PC 18:1_18:1 loses an 18:1 only
        Assert.DoesNotContain(result.Reactions, r => r.Reactant == "PC 36:2" && r.Product == "LPC 16:0");
        Assert.Contains(result.Reactions, r => r.Reactant == "LPC 18:1" && r.Product == "PC 36:2" && r.Id == "LPCAT");
        Assert.Contains(result.Reactions, r => r.Reactant == "DG 34:1" && r.Product == "PC 34:1");
        Assert.DoesNotContain(result.Reactions, r => r.Reactant == "DG 34:1" && r.Product == "PC 36:2");
        Assert.Empty(result.Predicted);
    }

    [Fact]
    public void Fatty_acid_level_sums_the_chains_and_links_them_by_elongation_and_desaturation()
    {
        var result = LipidPathways.Compute(Table(), "treated", "control", PathwayLevel.FattyAcid);

        var fa160 = result.Nodes.First(n => n.Name == "FA 16:0");
        var fa181 = result.Nodes.First(n => n.Name == "FA 18:1");
        // 16:0 sits in PE 34:1, PC 34:1, LPC 16:0 and DG 34:1; 18:1 in all seven species, twice in the 36:2s
        Assert.Equal(4, fa160.Members);
        Assert.Equal(8, fa181.Members);
        // 16:0 → 18:0 is not there (no 18:0 measured), 16:0 → 16:1 neither; 18:1 has nothing above it measured
        Assert.Empty(result.Reactions);
        Assert.Contains("No reaction", result.Message);
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
}
