using System.Globalization;
using System.Text;
using CompMs.Common.Components;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;

namespace MilX.Interop.OpenQuant;

/// <summary>One row of an OpenQuant component table (its Method workspace / CSV import).</summary>
public sealed class OpenQuantComponent
{
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public double Precursor { get; set; }
    public double? Fragment { get; set; }
    public double? Rt { get; set; }
    /// <summary>Half width of the retention-time window in minutes ("window" column).</summary>
    public double? RtHalfWidth { get; set; }
    public double Tolerance { get; set; } = 0.02;
    public string Unit { get; set; } = "Da";
    public string Formula { get; set; } = string.Empty;
    public string Adduct { get; set; } = string.Empty;
    public bool IsInternalStandard { get; set; }
    public string InternalStandard { get; set; } = string.Empty;
    public string Response { get; set; } = "area";
    public string ConcentrationUnit { get; set; } = string.Empty;
    public string QualifierOf { get; set; } = string.Empty;
    public double? IonRatio { get; set; }
    public double? IonRatioTolerance { get; set; }
    public string Regression { get; set; } = string.Empty;
    public string Weighting { get; set; } = string.Empty;
    public string LmId { get; set; } = string.Empty;
}

/// <summary>Options for turning MS-DIAL alignment spots into OpenQuant components.</summary>
public sealed class OpenQuantExportOptions
{
    /// <summary>Retention-time half window written to the "window" column (minutes).</summary>
    public double RtHalfWidthMinutes { get; set; } = 0.3;
    /// <summary>m/z tolerance for the XIC (Da or ppm according to <see cref="Unit"/>).</summary>
    public double Tolerance { get; set; } = 0.02;
    public string Unit { get; set; } = "Da";
    /// <summary>Use the most intense product ion of the representative MS/MS as the "fragment" (MRM-HR style) when available.</summary>
    public bool UseFragment { get; set; } = true;
    /// <summary>Skip spots without a compound name.</summary>
    public bool AnnotatedOnly { get; set; } = false;
    /// <summary>Product ions below this m/z distance from the precursor are not used as fragments (avoids the residual precursor).</summary>
    public double MinFragmentDistance { get; set; } = 2.0;
}

/// <summary>
/// Writes OpenQuant's component CSV (headers exactly as OpenQuant's <c>components.save_components</c>
/// writes and <c>load_components</c> reads), so a discovery list from MIL-X becomes a targeted
/// method in OpenQuant in one step.
/// </summary>
public static class OpenQuantComponentCsv
{
    public static readonly string[] Header = {
        "name", "group", "precursor", "fragment", "rt", "window", "tolerance", "unit", "formula", "adduct", "is",
        "internal_standard", "response", "concentration_unit", "qualifier_of", "ion_ratio", "ion_ratio_tolerance",
        "regression", "weighting", "lm_id",
    };

    private static string Fmt(double? value) => value is null ? string.Empty : value.Value.ToString("G12", CultureInfo.InvariantCulture);

    private static string Escape(string value) {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static void Write(TextWriter writer, IEnumerable<OpenQuantComponent> components) {
        writer.WriteLine(string.Join(",", Header));
        foreach (var c in components) {
            var fields = new[] {
                Escape(c.Name), Escape(c.Group), Fmt(c.Precursor), Fmt(c.Fragment), Fmt(c.Rt), Fmt(c.RtHalfWidth), Fmt(c.Tolerance),
                Escape(c.Unit), Escape(c.Formula), Escape(c.Adduct), c.IsInternalStandard ? "yes" : string.Empty,
                Escape(c.InternalStandard), Escape(c.Response), Escape(c.ConcentrationUnit), Escape(c.QualifierOf),
                Fmt(c.IonRatio), Fmt(c.IonRatioTolerance), Escape(c.Regression), Escape(c.Weighting), Escape(c.LmId),
            };
            writer.WriteLine(string.Join(",", fields));
        }
    }

    public static void Write(string path, IEnumerable<OpenQuantComponent> components) {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        Write(writer, components);
    }

    /// <summary>
    /// Maps MS-DIAL alignment spots to components. <paramref name="msdecResults"/> (aligned with
    /// <paramref name="spots"/>) supplies the representative MS/MS used for the fragment column.
    /// </summary>
    public static List<OpenQuantComponent> FromAlignmentSpots(IReadOnlyList<AlignmentSpotProperty> spots, IReadOnlyList<MSDecResult>? msdecResults = null, OpenQuantExportOptions? options = null) {
        options ??= new OpenQuantExportOptions();
        var result = new List<OpenQuantComponent>();
        for (var i = 0; i < spots.Count; i++) {
            var spot = spots[i];
            var name = spot.Name ?? string.Empty;
            var annotated = !string.IsNullOrWhiteSpace(name) && !name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase);
            if (options.AnnotatedOnly && !annotated) continue;
            var rt = spot.TimesCenter?.RT?.Value ?? 0.0;
            var mz = spot.MassCenter;
            var component = new OpenQuantComponent {
                Name = annotated ? name : $"m/z {mz.ToString("F4", CultureInfo.InvariantCulture)} @ {rt.ToString("F2", CultureInfo.InvariantCulture)} min",
                Group = annotated ? (string.IsNullOrWhiteSpace(spot.Ontology) ? "annotated" : spot.Ontology) : "unknown",
                Precursor = mz,
                Rt = rt > 0 ? rt : null,
                RtHalfWidth = rt > 0 ? options.RtHalfWidthMinutes : null,
                Tolerance = options.Tolerance,
                Unit = options.Unit,
                Formula = spot.Formula?.FormulaString ?? string.Empty,
                Adduct = spot.AdductType?.AdductIonName ?? string.Empty,
                Response = "area",
            };
            if (options.UseFragment && msdecResults != null && i < msdecResults.Count && msdecResults[i]?.Spectrum is { Count: > 0 } spectrum) {
                var best = spectrum
                    .Where(p => Math.Abs(p.Mass - mz) > options.MinFragmentDistance && p.Mass < mz)
                    .OrderByDescending(p => p.Intensity)
                    .FirstOrDefault();
                if (best != null) component.Fragment = best.Mass;
            }
            result.Add(component);
        }
        return result;
    }
}
