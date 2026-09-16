using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilX.Desktop.Controls;
using MilX.Desktop.Services;
using MilX.Pipeline.Statistics.Pathways;

namespace MilX.Desktop.ViewModels;

/// <summary>
/// The pathway analysis after BioPAN: the reactions of the mammalian lipid network weighted by
/// product over reactant in every injection, compared between two classes, and chained into
/// pathways. It reads the one-factor dataset — the confirmed analytes as ratios, normalised — and
/// the two classes chosen for the comparison, so it says the same thing about the same numbers as
/// the volcano plot does.
/// </summary>
public sealed partial class PathwaysViewModel : ViewModelBase
{
    private readonly OneFactorViewModel _analysis;
    private readonly IFileDialogService _dialogs;

    public PathwaysViewModel(OneFactorViewModel analysis, IFileDialogService dialogs)
    {
        _analysis = analysis;
        _dialogs = dialogs;
        _analysis.DataChanged += (_, _) => Clear();
    }

    public static string[] Levels { get; } = { "Lipid classes", "Molecular species", "Fatty acids" };
    /// <summary>BioPAN's thresholds: one-sided p, Z = Φ⁻¹(1 − p); 1.645 is its default.</summary>
    public static string[] Thresholds { get; } = { "1.282 (p 0.10)", "1.645 (p 0.05)", "2.054 (p 0.02)", "2.326 (p 0.01)" };

    [ObservableProperty] private string _level = Levels[0];
    [ObservableProperty] private string _threshold = Thresholds[1];
    [ObservableProperty] private string _maxPathLengthText = "3";
    [ObservableProperty] private bool _showUnchanged = true;
    /// <summary>Also the steps MIL-X adds beyond BioPAN's network: glycosphingolipids, sterol esters, acylcarnitines.</summary>
    [ObservableProperty] private bool _beyondBioPan;
    /// <summary>The injections of the two classes correspond one to one, in order: BioPAN's paired option.</summary>
    [ObservableProperty] private bool _paired;
    [ObservableProperty] private string _message = "Not computed yet.";
    [ObservableProperty] private PathwayResult? _result;
    [ObservableProperty] private IReadOnlyList<ReactionScore> _reactions = Array.Empty<ReactionScore>();
    [ObservableProperty] private IReadOnlyList<PathwayScore> _pathways = Array.Empty<PathwayScore>();
    [ObservableProperty] private IReadOnlyList<PredictedReaction> _predicted = Array.Empty<PredictedReaction>();
    [ObservableProperty] private IReadOnlyList<PathwayNode> _nodes = Array.Empty<PathwayNode>();
    [ObservableProperty] private ReactionScore? _selectedReaction;
    [ObservableProperty] private PathwayScore? _selectedPathway;
    [ObservableProperty] private IReadOnlyList<string>? _highlightedChain;
    [ObservableProperty] private IReadOnlyList<BoxGroup> _reactionBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private string _reactionTitle = string.Empty;
    [ObservableProperty] private string _reactionDetail = string.Empty;

    /// <summary>The number the threshold text names.</summary>
    public double ThresholdValue => double.TryParse(Threshold.Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ? z : 1.645;

    public void Clear()
    {
        Result = null;
        Reactions = Array.Empty<ReactionScore>();
        Pathways = Array.Empty<PathwayScore>();
        Predicted = Array.Empty<PredictedReaction>();
        Nodes = Array.Empty<PathwayNode>();
        SelectedReaction = null;
        SelectedPathway = null;
        HighlightedChain = null;
        ReactionBoxes = Array.Empty<BoxGroup>();
        ReactionTitle = string.Empty;
        ReactionDetail = string.Empty;
        Message = "Not computed yet.";
    }

    /// <summary>Scores the network between the two classes of the comparison.</summary>
    [RelayCommand]
    public void Compute()
    {
        var data = _analysis.Data;
        if (data is null) { Message = "Build the dataset on the Data processing page first."; return; }
        if (string.IsNullOrEmpty(_analysis.ClassA) || string.IsNullOrEmpty(_analysis.ClassB) || _analysis.ClassA == _analysis.ClassB)
        {
            Message = "Choose two different classes on the Statistical test page; the reactions are compared between them.";
            return;
        }
        var level = Level switch { "Molecular species" => PathwayLevel.Species, "Fatty acids" => PathwayLevel.FattyAcid, _ => PathwayLevel.Class };
        var length = int.TryParse(MaxPathLengthText?.Trim(), out var l) ? Math.Clamp(l, 1, 6) : 3;
        try
        {
            // the normalised linear values: a ratio of two abundances wants abundances, not their logs
            var result = LipidPathways.Compute(data.Normalized, _analysis.ClassA, _analysis.ClassB, level, ThresholdValue, length, includeExtensions: BeyondBioPan, paired: Paired);
            Result = result;
            Reactions = result.Reactions.OrderByDescending(r => r.AbsZ).ToList();
            Pathways = result.Pathways;
            Predicted = result.Predicted;
            Nodes = result.Nodes;
            Message = result.Message;
            SelectedPathway = null;
            HighlightedChain = null;
            SelectedReaction = Reactions.FirstOrDefault(r => r.Tested);
        }
        catch (Exception ex)
        {
            Message = "The pathway analysis failed: " + ex.Message;
        }
    }

    partial void OnSelectedReactionChanged(ReactionScore? value)
    {
        if (value is null || Result is null)
        {
            ReactionBoxes = Array.Empty<BoxGroup>();
            ReactionTitle = string.Empty;
            ReactionDetail = string.Empty;
            return;
        }
        // the weight of the reaction in every injection of each class: what the test compared
        static List<double> Finite(IReadOnlyList<double> weights) => weights.Where(w => !double.IsNaN(w)).ToList();
        ReactionBoxes = new[]
        {
            new BoxGroup(Result.ClassA, Finite(value.WeightsA), Result.ClassA),
            new BoxGroup(Result.ClassB, Finite(value.WeightsB), Result.ClassB),
        };
        ReactionTitle = $"{value.Label} · {value.Note}";
        ReactionDetail = value.Tested
            ? $"{value.Product}/{value.Reactant} is {Math.Pow(2, value.Log2Change):0.00}× ({value.Log2Change:+0.00;-0.00} log2) in {Result.ClassA} against {Result.ClassB} · p {value.P:0.0E0} · Z {value.Z:0.00} · {value.Status} · genes {value.GeneText}"
            : $"Not testable: fewer than two injections in a class have both {value.Reactant} and {value.Product} · genes {value.GeneText}";
    }

    partial void OnSelectedPathwayChanged(PathwayScore? value)
    {
        HighlightedChain = value?.Nodes;
        if (value is not null && value.Reactions.Count > 0) SelectedReaction = value.Reactions[0];
    }

    /// <summary>Writes one of the tables as tab-separated text.</summary>
    [RelayCommand]
    private async Task ExportTable(string? which)
    {
        if (Result is null) { Message = "Compute the pathways first."; return; }
        var adjustment = $"Z at |Z| ≥ {Result.Threshold:0.###}";
        var (name, header, rows) = which switch
        {
            "pathways" => ("pathways", new[] { "Pathway", "Reactions", "Z", "Status", "Genes" },
                Pathways.Select(p => new object[] { p.Chain, p.Length, p.Z, p.Status, p.GeneText })),
            "predicted" => ("predicted-reactions", new[] { "Reaction", "Measured", "Not measured", "Genes" },
                Predicted.Select(p => new object[] { p.Reaction, p.Present, p.Missing, p.GeneText })),
            _ => ("reactions", new[] { "Reaction", "Reactant", "Product", $"log2 change ({Result.ClassA} over {Result.ClassB})", "p", adjustment, "Status", "Genes", "Enzyme" },
                Reactions.Select(r => new object[] { r.Id, r.Reactant, r.Product, r.Log2Change, r.P, r.Z, r.Status, r.GeneText, r.Note })),
        };
        var path = await _dialogs.SaveFileAsync("Export the table", name + ".tsv", "tsv");
        if (path is null) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', header));
        foreach (var row in rows) sb.AppendLine(string.Join('\t', row.Select(Cell)));
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(false));
        Message = $"Written to {path}";
    }

    private static string Cell(object value) => value switch
    {
        double d => double.IsNaN(d) ? string.Empty : d.ToString("G6", CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty,
    };
}
