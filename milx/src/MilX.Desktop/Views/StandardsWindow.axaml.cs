using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using MilX.Pipeline.Results;
using MilX.Pipeline.Statistics;

namespace MilX.Desktop.Views;

/// <summary>One choice of standard: a confirmed feature, described so the list reads well.</summary>
public sealed record StandardChoice(int? FeatureId, string Text)
{
    public override string ToString() => Text;
}

/// <summary>One class of the dialog with the choices it can pick from.</summary>
public sealed partial class StandardRow : ObservableObject
{
    public StandardRow(string cls, int confirmed, IReadOnlyList<StandardChoice> choices, StandardChoice selected)
    {
        Class = cls;
        Confirmed = confirmed;
        Choices = choices;
        _selected = selected;
    }

    public string Class { get; }
    public int Confirmed { get; }
    public IReadOnlyList<StandardChoice> Choices { get; }
    [ObservableProperty] private StandardChoice _selected;
}

public partial class StandardsWindow : Window
{
    private readonly IReadOnlyList<AlignmentSpotRow> _confirmed;
    private readonly List<StandardRow> _rows = new();

    public StandardsWindow() : this(Array.Empty<AlignmentSpotRow>(), Array.Empty<StandardAssignment>()) { }

    public StandardsWindow(IReadOnlyList<AlignmentSpotRow> confirmed, IReadOnlyList<StandardAssignment> current)
    {
        InitializeComponent();
        _confirmed = confirmed;
        var classes = RelativeAbundance.ClassesWithConfirmed(confirmed);
        var none = new StandardChoice(null, "none — keep the raw area");
        foreach (var (cls, count) in classes)
        {
            // the class's own candidates first, best first, then every other confirmed feature by name
            var ranked = confirmed
                .Select(f => (Feature: f, Score: LipidNames.StandardScore(f.Name, f.Ontology, cls)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => string.Equals(RelativeAbundance.ClassOf(x.Feature), cls, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x.Feature.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new StandardChoice(x.Feature.Id, Describe(x.Feature, x.Score)))
                .ToList();
            var choices = new List<StandardChoice> { none };
            choices.AddRange(ranked);
            var currentId = current.FirstOrDefault(a => string.Equals(a.Class, cls, StringComparison.OrdinalIgnoreCase))?.StandardFeatureId;
            var selected = choices.FirstOrDefault(c => c.FeatureId == currentId) ?? none;
            var row = new StandardRow(cls, count, choices, selected);
            row.PropertyChanged += (_, _) => UpdateSummary();
            _rows.Add(row);
        }
        Rows.ItemsSource = _rows;
        UpdateSummary();
    }

    private static string Describe(AlignmentSpotRow f, int score)
    {
        var mark = score >= 100 ? "standard of this class" : score >= 40 ? "odd chain, this class" : score >= 20 ? "standard of another class" : RelativeAbundance.ClassOf(f);
        return $"{f.Name}  ·  {mark}  ·  RT {f.Rt:F2}, m/z {f.Mz:F4}, mean height {Controls.ChartBase.FormatIntensity(f.AverageHeight)}";
    }

    private void UpdateSummary()
    {
        var chosen = _rows.Count(r => r.Selected.FeatureId is not null);
        Summary.Text = $"{chosen} of {_rows.Count} class(es) with a standard";
    }

    /// <summary>What was chosen, or null when the dialog was cancelled.</summary>
    public IReadOnlyList<StandardAssignment>? Result { get; private set; }

    private void OnUse(object? sender, RoutedEventArgs e)
    {
        Result = _rows.Select(r => new StandardAssignment(r.Class, r.Selected.FeatureId)).ToList();
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnSuggest(object? sender, RoutedEventArgs e)
    {
        var suggested = RelativeAbundance.Suggest(_confirmed);
        foreach (var row in _rows)
        {
            var id = suggested.FirstOrDefault(a => string.Equals(a.Class, row.Class, StringComparison.OrdinalIgnoreCase))?.StandardFeatureId;
            row.Selected = row.Choices.FirstOrDefault(c => c.FeatureId == id) ?? row.Choices[0];
        }
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.Selected = row.Choices[0];
    }
}
