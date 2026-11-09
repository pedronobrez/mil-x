using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MilX.Cli;

/// <summary>
/// The answer to one request: a JSON document that starts with the contract's envelope, or its TSV
/// rendering. Numbers are rounded here — m/z to 4 decimals (6 for theoretical values), retention
/// times to 4 in minutes — so a document is the same bytes however it was asked for.
/// </summary>
public sealed class Document
{
    public const string Schema = "mil-cli/1";
    public const string Tool = "milx";

    public Document(string command, JsonObject? input = null)
    {
        Root = new JsonObject
        {
            ["schema"] = Schema,
            ["tool"] = Tool,
            ["version"] = Cli.Version,
            ["command"] = command,
            ["input"] = input ?? new JsonObject(),
        };
    }

    public JsonObject Root { get; }
    public JsonObject Input => (JsonObject)Root["input"]!;

    /// <summary>Rows for the TSV rendering: one dictionary per row, the first row's keys as the header.</summary>
    public List<JsonObject>? Rows { get; set; }

    /// <summary>Files written beside the document (traces, MSP, TSV), for the server's listing.</summary>
    public List<string> Files { get; } = new();

    /// <summary>The serializer options every document is written with; the resolver is named so the options work whatever the host process set up.</summary>
    public static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
    public static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    public string ToJson() => Root.ToJsonString(Pretty) + "\n";

    public string ToTsv()
    {
        var rows = Rows ?? new List<JsonObject>();
        var sb = new StringBuilder();
        var header = rows.Count > 0 ? rows[0].Select(kv => kv.Key).ToList() : new List<string>();
        sb.Append(string.Join('\t', header)).Append('\n');
        foreach (var row in rows)
        {
            sb.Append(string.Join('\t', header.Select(h => Cell(row[h])))).Append('\n');
        }
        return sb.ToString();
    }

    private static string Cell(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue v when v.TryGetValue<double>(out var d) => d.ToString("R", CultureInfo.InvariantCulture),
        JsonValue v when v.TryGetValue<string>(out var s) => s.Replace('\t', ' ').Replace('\n', ' '),
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v when v.TryGetValue<int>(out var i) => i.ToString(CultureInfo.InvariantCulture),
        JsonValue v when v.TryGetValue<long>(out var l) => l.ToString(CultureInfo.InvariantCulture),
        _ => node.ToJsonString(),
    };

    public string Render(string format) => string.Equals(format, "tsv", StringComparison.OrdinalIgnoreCase) ? ToTsv() : ToJson();
}

/// <summary>The contract's rounding, in one place.</summary>
public static class Num
{
    /// <summary>A measured m/z: 4 decimals.</summary>
    public static double Mz(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    /// <summary>A theoretical m/z: 6 decimals.</summary>
    public static double TheoryMz(double v) => Math.Round(v, 6, MidpointRounding.AwayFromZero);
    /// <summary>A retention time in minutes: 4 decimals.</summary>
    public static double Rt(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    /// <summary>A ratio or score: 4 decimals.</summary>
    public static double Ratio(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    /// <summary>An intensity: as the reader reports it, but never NaN or infinity in a document.</summary>
    public static double? Intensity(double v) => double.IsNaN(v) || double.IsInfinity(v) ? null : v;

    public static JsonArray Array(IEnumerable<double> values)
    {
        var a = new JsonArray();
        foreach (var v in values) a.Add(v);
        return a;
    }
}
