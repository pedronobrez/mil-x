using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MilX.Pipeline.Curation;
using MilX.Pipeline.Results;

namespace MilX.Desktop.ViewModels;

/// <summary>
/// What the polarity pairing knows about one feature: which polarity this run is, and the compound
/// the other polarity saw, when it saw it at all.
/// </summary>
public sealed record PolarityState(
    bool OwnIsPositive,
    PolarityPair? Pair,
    double PartnerMz,
    double PartnerRt,
    double PartnerSignalToNoise,
    string PartnerName);

/// <summary>
/// One row of the ion table: an aligned feature plus what the reviewer has decided about it.
/// The decision lives in the <see cref="CurationStore"/> so it survives the session and is
/// readable by MS-DIAL; this class is the observable face of it.
/// </summary>
public sealed partial class SpotRowViewModel : ObservableObject
{
    private readonly CurationStore _store;

    public SpotRowViewModel(AlignmentSpotRow spot, CurationStore store)
    {
        Spot = spot;
        _store = store;
    }

    public AlignmentSpotRow Spot { get; }

    public int Id => Spot.Id;
    public double Rt => Spot.Rt;
    public double Mz => Spot.Mz;
    public string Adduct => Spot.Adduct;
    public string Ontology => Spot.Ontology;
    public string Formula => Spot.Formula;
    public double Fill => Spot.FillPercent;
    public double Score => Spot.Score;
    public double Height => Spot.AverageHeight;
    public double SignalToNoise => Spot.SignalToNoiseAverage;

    /// <summary>
    /// How much of this feature the blanks carry: the mean height over the injections typed Blank
    /// against the mean over the real samples, as a percentage. A hundred means the blank is as
    /// high as the samples, which is the first thing to throw away in any untargeted run. NaN when
    /// the batch has no blank, so a run without one is not quietly filtered on nothing.
    /// </summary>
    public double BlankPercent
    {
        get
        {
            double blank = 0, sample = 0;
            int blanks = 0, samples = 0;
            foreach (var p in Spot.SamplePeaks)
            {
                var height = double.IsNaN(p.Height) ? 0 : p.Height;
                if (string.Equals(p.SampleType, "Blank", StringComparison.OrdinalIgnoreCase)) { blank += height; blanks++; }
                else if (!string.Equals(p.SampleType, "QC", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(p.SampleType, "Standard", StringComparison.OrdinalIgnoreCase)) { sample += height; samples++; }
            }
            if (blanks == 0 || samples == 0) return double.NaN;
            var meanSample = sample / samples;
            if (meanSample <= 0) return blank > 0 ? 999 : double.NaN;
            return blank / blanks / meanSample * 100;
        }
    }

    /// <summary>
    /// The neutral molecule behind this ion, from the adduct the run assigned it. NaN when the
    /// adduct is one the adduct table does not know, which is the honest answer rather than a
    /// protonated guess: it is the neutral mass that identifies a compound across the polarities.
    /// </summary>
    public double NeutralMass => PolarityLink.TryNeutral(Mz, Adduct, out var neutral) ? neutral : double.NaN;

    public string NeutralText => double.IsNaN(NeutralMass) ? string.Empty : NeutralMass.ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>What the other polarity of this batch saw here; null until a polarity is linked.</summary>
    public PolarityState? Polarity { get; set; }

    /// <summary>True when the same compound was found in both polarities of the batch.</summary>
    public bool SeenInBoth => Polarity?.Pair is not null;

    /// <summary>The polarity column: ± when both runs saw it, otherwise the one that did.</summary>
    public string PolarityText => Polarity is null ? string.Empty
        : Polarity.Pair is not null ? "\u00b1"
        : Polarity.OwnIsPositive ? "+" : "\u2212";

    /// <summary>The partner, as the ion table prints it: what it was there, and how well it agrees.</summary>
    public string PartnerText
    {
        get
        {
            if (Polarity?.Pair is not { } pair) return string.Empty;
            var name = Polarity.PartnerName.Length > 0 && !Polarity.PartnerName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
                ? Polarity.PartnerName + "  "
                : string.Empty;
            var correlation = double.IsNaN(pair.Correlation) ? string.Empty : $" \u00b7 r {pair.Correlation:0.00}";
            return $"{name}{Polarity.PartnerMz.ToString("F4", CultureInfo.InvariantCulture)} @ {Polarity.PartnerRt.ToString("F2", CultureInfo.InvariantCulture)}{correlation}";
        }
    }

    /// <summary>Why the pairing believes these two are one compound.</summary>
    public string PartnerExplanation => Polarity?.Pair?.Explanation ?? string.Empty;

    /// <summary>
    /// Which polarity quantifies this compound: the one that measured it better. Heights are never
    /// added across the polarities, so one side carries the number and the other confirms it.
    /// </summary>
    public string QuantifyText => Polarity?.Pair is not { } pair ? string.Empty
        : pair.Quantify == PolarityChoice.Positive ? "positive" : "negative";

    /// <summary>True when this row is the side the compound is quantified from.</summary>
    public bool QuantifiesHere => Polarity?.Pair is { } pair &&
        (pair.Quantify == PolarityChoice.Positive) == Polarity.OwnIsPositive;

    /// <summary>Said again after a polarity is linked or unlinked.</summary>
    public void RaisePolarityChanged()
    {
        OnPropertyChanged(nameof(Polarity));
        OnPropertyChanged(nameof(SeenInBoth));
        OnPropertyChanged(nameof(PolarityText));
        OnPropertyChanged(nameof(PartnerText));
        OnPropertyChanged(nameof(PartnerExplanation));
        OnPropertyChanged(nameof(QuantifyText));
        OnPropertyChanged(nameof(QuantifiesHere));
    }

    /// <summary>
    /// Which compound this ion belongs to, once the adducts and isotopes have been gathered. Null
    /// until the grouping has run, and its own id when it is the ion that represents the compound.
    /// </summary>
    public IonGroup? Group { get; set; }

    /// <summary>The group as the ion table prints it: empty for the representative, else what it is.</summary>
    public string GroupText => Group is null || Group.IsRepresentative ? string.Empty : $"{Group.Relation} · {Group.Explanation}";

    /// <summary>The same, as the ion table prints it.</summary>
    public string BlankText => double.IsNaN(BlankPercent) ? string.Empty : BlankPercent >= 999 ? "999+" : BlankPercent.ToString("0", CultureInfo.InvariantCulture);
    public bool MsmsAssigned => Spot.MsmsAssigned;
    public IReadOnlyList<ClassHeight> ClassHeights => Spot.ClassHeights;
    /// <summary>Not an isotope of another feature: what MS-DIAL's "molecular ion" filter keeps.</summary>
    public bool IsMolecularIon => Spot.IsotopeWeight <= 0;
    public string IsotopeText => Spot.IsotopeWeight switch { < 0 => string.Empty, 0 => "M", var n => "M+" + n };
    /// <summary>Touched by hand, either the annotation or the integration.</summary>
    public bool IsManuallyEdited => IsManual || Spot.IsManuallyAnnotated || Spot.IsManuallyQuantified;
    public string MsmsText => Spot.MsmsAssigned ? "MS/MS" : string.Empty;

    /// <summary>The reviewer's name when they picked one, otherwise the annotation the run produced.</summary>
    public string Name
    {
        get
        {
            var manual = _store.Get(Id).ManualName;
            return manual.Length > 0 ? manual : Spot.Name;
        }
    }

    public string DisplayName => Name.Length > 0 && !Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
        ? Name
        : "Unknown  m/z " + Mz.ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>Confidence MS-DIAL expressed in the name: a plain name, a suggestion, or nothing.</summary>
    public string Level => Name.StartsWith("low score:", StringComparison.OrdinalIgnoreCase) ? "suggested"
        : Name.StartsWith("no MS2:", StringComparison.OrdinalIgnoreCase) ? "m/z only"
        : Name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase) || Name.Length == 0 || Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) ? string.Empty
        : "confident";

    public bool IsAnnotated => Level.Length > 0;
    public bool IsConfident => Level == "confident";
    public bool IsManual => _store.Get(Id).ManualName.Length > 0;

    public string Comment
    {
        get => _store.Get(Id).Comment;
        set
        {
            if (Comment == value) return;
            _store.SetComment(Id, value ?? string.Empty);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasComment));
        }
    }

    public bool HasComment => Comment.Length > 0;

    public bool Reviewed
    {
        get => _store.Get(Id).Reviewed || _store.TagsOf(Id).Count > 0;
        set
        {
            _store.SetReviewed(Id, value);
            OnPropertyChanged();
        }
    }

    public bool HasTag(PeakSpotTagKind tag) => _store.HasTag(Id, tag);

    public void SetTag(PeakSpotTagKind tag, bool on)
    {
        _store.SetTag(Id, tag, on);
        RaiseTagChanged();
    }

    public void ToggleTag(PeakSpotTagKind tag) => SetTag(tag, !HasTag(tag));

    public void ClearTags()
    {
        _store.ClearTags(Id);
        RaiseTagChanged();
    }

    public void SetManualName(string name)
    {
        _store.SetManualName(Id, name);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IsAnnotated));
        OnPropertyChanged(nameof(IsConfident));
        OnPropertyChanged(nameof(IsManual));
    }

    private void RaiseTagChanged()
    {
        OnPropertyChanged(nameof(TagText));
        OnPropertyChanged(nameof(IsConfirmed));
        OnPropertyChanged(nameof(IsRejected));
        OnPropertyChanged(nameof(IsLowQuality));
        OnPropertyChanged(nameof(IsCoelution));
        OnPropertyChanged(nameof(IsOverannotation));
        OnPropertyChanged(nameof(IsFlagged));
        OnPropertyChanged(nameof(HasAnyTag));
        OnPropertyChanged(nameof(Reviewed));
    }

    /// <summary>The tags as one cell, shortest first so the column stays readable.</summary>
    public string TagText => string.Join(", ", _store.TagsOf(Id).OrderBy(t => (int)t).Select(t => t.ShortLabel()));

    public bool HasAnyTag => _store.TagsOf(Id).Count > 0;
    public bool IsConfirmed => HasTag(PeakSpotTagKind.Confirmed);
    public bool IsRejected => HasTag(PeakSpotTagKind.Misannotation);
    public bool IsLowQuality => HasTag(PeakSpotTagKind.LowQualitySpectrum);
    public bool IsCoelution => HasTag(PeakSpotTagKind.Coelution);
    public bool IsOverannotation => HasTag(PeakSpotTagKind.Overannotation);
    /// <summary>Tagged as something to come back to rather than accepted or rejected outright.</summary>
    public bool IsFlagged => HasTag(PeakSpotTagKind.LowQualitySpectrum) || HasTag(PeakSpotTagKind.Coelution) || HasTag(PeakSpotTagKind.Overannotation);

    /// <summary>
    /// Everything this row reads from the curation store, said again: when the row is re-shown, and
    /// after an undo, which can change any of it.
    /// </summary>
    public void Refresh()
    {
        RaiseTagChanged();
        OnPropertyChanged(nameof(Comment));
        OnPropertyChanged(nameof(HasComment));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IsAnnotated));
        OnPropertyChanged(nameof(IsConfident));
        OnPropertyChanged(nameof(IsManual));
    }
}
