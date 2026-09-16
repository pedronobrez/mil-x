using System.Globalization;
using System.Text.Json;
using CompMs.Common.Enum;

namespace MilX.Interop.OpenQuant;

/// <summary>One injection of an OpenQuant batch (its Samples workspace).</summary>
public sealed record OpenQuantSample(
    string Path,
    int SampleIndex,
    string Name,
    string SampleType,
    string SampleGroup,
    double? ActualConcentration,
    double DilutionFactor,
    string Comment)
{
    /// <summary>OpenQuant sample type → MS-DIAL analysis file type.</summary>
    public AnalysisFileType ToAnalysisFileType() => SampleType switch {
        "Standard" => AnalysisFileType.Standard,
        "Quality Control" => AnalysisFileType.QC,
        "Blank" or "Double Blank" or "Solvent" => AnalysisFileType.Blank,
        _ => AnalysisFileType.Sample,
    };

    /// <summary>MS-DIAL class: the study group, or the sample type when no group was given.</summary>
    public string ToAnalysisClass() => string.IsNullOrWhiteSpace(SampleGroup) ? SampleType : SampleGroup;
}

/// <summary>Reads the sample table (and, when present, the component table) of an OpenQuant project (.oqproj / .opvproj JSON).</summary>
public static class OpenQuantProject
{
    public static readonly string[] Extensions = { ".oqproj", ".opvproj" };

    public static bool IsProject(string path) => Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    public sealed class Contents
    {
        public int Version { get; init; }
        public List<OpenQuantSample> Samples { get; init; } = new();
        public List<OpenQuantComponent> Components { get; init; } = new();
    }

    public static Contents Load(string path) {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var contents = new Contents { Version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vi) ? vi : 0 };
        if (root.TryGetProperty("samples", out var samples) && samples.ValueKind == JsonValueKind.Array) {
            foreach (var row in samples.EnumerateArray()) {
                contents.Samples.Add(new OpenQuantSample(
                    Str(row, "path"),
                    Int(row, "sample_index") ?? 0,
                    Str(row, "name"),
                    string.IsNullOrEmpty(Str(row, "sample_type")) ? "Unknown" : Str(row, "sample_type"),
                    Str(row, "sample_group"),
                    Dbl(row, "actual_concentration"),
                    Dbl(row, "dilution_factor") ?? 1.0,
                    Str(row, "comment")));
            }
        }
        if (root.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.Object
            && method.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array) {
            foreach (var row in comps.EnumerateArray()) {
                contents.Components.Add(new OpenQuantComponent {
                    Name = Str(row, "name"),
                    Group = Str(row, "group"),
                    Precursor = Dbl(row, "precursor") ?? 0,
                    Fragment = Dbl(row, "fragment"),
                    Rt = Dbl(row, "rt"),
                    RtHalfWidth = Dbl(row, "rt_halfwidth") ?? Dbl(row, "window"),
                    Tolerance = Dbl(row, "tolerance") ?? 0.02,
                    Unit = string.IsNullOrEmpty(Str(row, "unit")) ? "Da" : Str(row, "unit"),
                    Formula = Str(row, "formula"),
                    Adduct = Str(row, "adduct"),
                    IsInternalStandard = Bool(row, "is_internal_standard"),
                    InternalStandard = Str(row, "internal_standard"),
                    Response = string.IsNullOrEmpty(Str(row, "response")) ? "area" : Str(row, "response"),
                    ConcentrationUnit = Str(row, "concentration_unit"),
                    QualifierOf = Str(row, "qualifier_of"),
                    IonRatio = Dbl(row, "ion_ratio"),
                    LmId = Str(row, "lm_id"),
                });
            }
        }
        return contents;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty
        : e.TryGetProperty(name, out p) && p.ValueKind is JsonValueKind.Number ? p.GetRawText() : string.Empty;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i) ? i : null;

    private static double? Dbl(JsonElement e, string name) {
        if (!e.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && (p.ValueKind == JsonValueKind.True || (p.ValueKind == JsonValueKind.String && p.GetString() is "yes" or "true" or "1"));
}
