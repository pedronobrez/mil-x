using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>
/// Two factors at once: treatment and time, genotype and diet, class and batch. The two-way
/// analysis of variance per feature and ASCA over the whole matrix, on the one-factor dataset —
/// the same values every other page reads — with the factors taken from the sample table: the
/// class, the second factor typed in the Samples workspace, the batch, or the sample type.
/// </summary>
public sealed partial class TwoFactorViewModel : ViewModelBase
{
    private readonly OneFactorViewModel _analysis;
    private readonly IFileDialogService _dialogs;
    private TwoWayResult? _anova;
    private AscaResult? _asca;

    public TwoFactorViewModel(OneFactorViewModel analysis, IFileDialogService dialogs)
    {
        _analysis = analysis;
        _dialogs = dialogs;
        _analysis.DataChanged += (_, _) => Clear();
    }

    public static string[] FactorSources { get; } = { "Class", "Factor (Samples workspace)", "Batch", "Sample type" };

    [ObservableProperty] private string _factorA = FactorSources[0];
    [ObservableProperty] private string _factorB = FactorSources[1];
    [ObservableProperty] private bool _interaction = true;
    [ObservableProperty] private string _adjustment = OneFactorViewModel.Adjustments[0];
    [ObservableProperty] private string _alphaText = "0.05";
    [ObservableProperty] private string _permutationsText = "200";
    [ObservableProperty] private string _message = "Not computed yet.";
    [ObservableProperty] private string _ascaMessage = string.Empty;
    [ObservableProperty] private IReadOnlyList<TwoWayFeature> _rows = Array.Empty<TwoWayFeature>();
    [ObservableProperty] private TwoWayFeature? _selectedRow;
    [ObservableProperty] private IReadOnlyList<BoxGroup> _cellBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private string _cellTitle = string.Empty;
    [ObservableProperty] private IReadOnlyList<RankItem> _effectBars = Array.Empty<RankItem>();
    [ObservableProperty] private IReadOnlyList<string> _effectNames = Array.Empty<string>();
    [ObservableProperty] private string? _selectedEffect;
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _effectScores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private string _effectLabel = string.Empty;
    [ObservableProperty] private string _effectYLabel = "PC2 of the effect";
    [ObservableProperty] private bool _isComputing;
    [ObservableProperty] private string _significantColumn = "Significant";

    public string NameA => FactorA.Split(' ')[0];
    public string NameB => FactorB.Split(' ')[0];

    public void Clear()
    {
        _anova = null;
        _asca = null;
        Rows = Array.Empty<TwoWayFeature>();
        SelectedRow = null;
        CellBoxes = Array.Empty<BoxGroup>();
        CellTitle = string.Empty;
        EffectBars = Array.Empty<RankItem>();
        EffectNames = Array.Empty<string>();
        SelectedEffect = null;
        EffectScores = Array.Empty<ScatterPoint>();
        EffectLabel = string.Empty;
        Message = "Not computed yet.";
        AscaMessage = string.Empty;
    }

    /// <summary>The level of one factor for every injection of the dataset.</summary>
    private static IReadOnlyList<string> Levels(string source, IReadOnlyList<SampleInfo> samples) => samples.Select(s => source switch
    {
        "Batch" => s.Batch.ToString(CultureInfo.InvariantCulture),
        "Sample type" => s.SampleType ?? string.Empty,
        var x when x.StartsWith("Factor", StringComparison.Ordinal) => s.Factor ?? string.Empty,
        _ => AnalysisTable.ClassOf(s),
    }).ToList();

    private PAdjustment AdjustmentKind => Adjustment switch { "Holm" => PAdjustment.Holm, "Bonferroni" => PAdjustment.Bonferroni, "None (raw p)" => PAdjustment.None, _ => PAdjustment.FalseDiscoveryRate };
    private double Alpha => double.TryParse(AlphaText?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? Math.Clamp(a, 1e-9, 1) : 0.05;

    /// <summary>The two-way ANOVA on the transformed values, then ASCA on the scaled ones, in the background.</summary>
    [RelayCommand]
    private async Task Compute()
    {
        var data = _analysis.Data;
        if (data is null) { Message = "Build the dataset on the Data processing page first."; return; }
        if (FactorA == FactorB) { Message = "Choose two different factors."; return; }
        var samples = data.Transformed.Samples;
        var a = Levels(FactorA, samples);
        var b = Levels(FactorB, samples);
        if (b.All(string.IsNullOrEmpty) && FactorB.StartsWith("Factor", StringComparison.Ordinal))
        {
            Message = "No injection has a second factor yet: type it in the Factor column of the Samples workspace (the time point, the diet, the genotype), save, and reload from the review.";
            return;
        }
        var permutations = int.TryParse(PermutationsText?.Trim(), out var k) ? Math.Clamp(k, 0, 5000) : 200;
        var interaction = Interaction;
        var adjustment = AdjustmentKind;
        var nameA = NameA;
        var nameB = NameB;
        IsComputing = true;
        Message = "Fitting every feature, then partitioning the matrix…";
        try
        {
            var (anova, asca) = await Task.Run(() =>
                (TwoFactor.Anova(data.Transformed, a, b, nameA, nameB, interaction, adjustment),
                 TwoFactor.Asca(data.Scaled, a, b, nameA, nameB, interaction, permutations)));
            _anova = anova;
            _asca = asca;
            Rows = anova.Features.OrderBy(f => double.IsNaN(f.BestAdjustedP) ? 2 : f.BestAdjustedP).ToList();
            Message = anova.Message;
            SignificantColumn = $"≤ {Alpha:0.###}";
            SelectedRow = Rows.FirstOrDefault();
            AscaMessage = asca.Message;
            var bars = asca.Effects.Select(e => new RankItem(e.Name, e.PercentOfVariation, e.PermutationP <= 0.05 ? "significant" : "not significant", e, null, $"permutation p {e.PermutationP:0.000}")).ToList();
            if (!double.IsNaN(asca.ResidualPercent)) bars.Add(new RankItem("residual", asca.ResidualPercent, "residual", null, null, "what no factor explains"));
            EffectBars = bars;
            EffectNames = asca.Effects.Select(e => e.Name).ToList();
            SelectedEffect = EffectNames.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Message = "The two-factor analysis failed: " + ex.Message;
        }
        finally
        {
            IsComputing = false;
        }
    }

    partial void OnSelectedRowChanged(TwoWayFeature? value)
    {
        var data = _analysis.Data;
        if (value is null || _anova is null || data is null)
        {
            CellBoxes = Array.Empty<BoxGroup>();
            CellTitle = string.Empty;
            return;
        }
        // one box per cell of the design, coloured by the second factor, in the order of the first
        var samples = data.Transformed.Samples;
        var a = Levels(FactorA, samples);
        var b = Levels(FactorB, samples);
        var j = data.Transformed.Features.ToList().FindIndex(f => f.Id == value.FeatureId);
        if (j < 0) return;
        var boxes = new List<BoxGroup>();
        foreach (var la in _anova.LevelsA)
            foreach (var lb in _anova.LevelsB)
            {
                var values = Enumerable.Range(0, samples.Count).Where(i => a[i] == la && b[i] == lb).Select(i => data.Transformed.Values[i, j]).Where(v => !double.IsNaN(v)).ToList();
                boxes.Add(new BoxGroup($"{la} · {lb}", values, lb));
            }
        CellBoxes = boxes;
        CellTitle = $"{value.Label} · {_anova.NameA} p {value.AdjustedPA:0.0E0} · {_anova.NameB} p {value.AdjustedPB:0.0E0}" + (_anova.Interaction ? $" · interaction p {value.AdjustedPAB:0.0E0}" : string.Empty);
    }

    partial void OnSelectedEffectChanged(string? value)
    {
        var effect = _asca?.Effects.FirstOrDefault(e => e.Name == value);
        if (effect is null)
        {
            EffectScores = Array.Empty<ScatterPoint>();
            EffectLabel = string.Empty;
            return;
        }
        // the injections on the effect's first two components, coloured by the factor the effect is about
        var byA = effect.Name == _asca!.Effects[0].Name;
        EffectScores = effect.Scores.Select(s => new ScatterPoint(s.Pc1, s.Pc2, s.Sample, byA ? s.LevelA : effect.Name.Contains('×') ? $"{s.LevelA} · {s.LevelB}" : s.LevelB, s) { Labelled = false }).ToList();
        EffectYLabel = effect.SecondAxisLabel;
        EffectLabel = $"{effect.Name}: {effect.PercentOfVariation:F1} % of the variation, permutation p {effect.PermutationP:0.000}"
            + (effect.SecondAxisIsResidual ? " · one component (two levels); the residual's first is the vertical axis"
               : effect.Explained.Count > 1 ? $" · PC1 {effect.Explained[0]:F0} %, PC2 {effect.Explained[1]:F0} % of the effect" : string.Empty);
    }

    /// <summary>Writes the ANOVA table as tab-separated text.</summary>
    [RelayCommand]
    private async Task ExportTable()
    {
        if (_anova is null) { Message = "Compute first."; return; }
        var path = await _dialogs.SaveFileAsync("Export the table", "two-way-anova.tsv", "tsv");
        if (path is null) return;
        var adj = Univariate.AdjustName(AdjustmentKind);
        var header = new List<string> { "Feature id", "Feature", "Class", $"F {_anova.NameA}", $"p {_anova.NameA}", $"{adj} {_anova.NameA}", $"F {_anova.NameB}", $"p {_anova.NameB}", $"{adj} {_anova.NameB}" };
        if (_anova.Interaction) header.AddRange(new[] { "F interaction", "p interaction", $"{adj} interaction" });
        foreach (var la in _anova.LevelsA) foreach (var lb in _anova.LevelsB) header.Add($"mean {la} · {lb}");
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', header));
        foreach (var r in Rows)
        {
            var cells = new List<object> { r.FeatureId, r.Label, r.Group, r.FA, r.PA, r.AdjustedPA, r.FB, r.PB, r.AdjustedPB };
            if (_anova.Interaction) cells.AddRange(new object[] { r.FAB, r.PAB, r.AdjustedPAB });
            cells.AddRange(r.CellMeans.Cast<object>());
            sb.AppendLine(string.Join('\t', cells.Select(Cell)));
        }
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(false));
        Message = $"Written to {path}";
    }

    private static string Cell(object value) => value switch
    {
        double d => double.IsNaN(d) ? string.Empty : d.ToString("G6", CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty,
    };
}
