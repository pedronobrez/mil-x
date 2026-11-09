using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using CompMs.App.MsdialConsole.Process;

namespace MilX.Cli.Commands;

/// <summary>
/// The MS-DIAL console's workflows (lcms, gcms, …) run in-process, with three things an agent needs
/// beyond what the console takes: <c>--acquisition-type DDA|SWATH</c> and <c>--library X.msp</c> override
/// the method file (the MS-DIAL 5.5 projects of this user's IDA data were marked SWATH), and
/// <c>--summary</c> prints a compact JSON of what the run produced — peaks, aligned features, MS/MS
/// assigned, annotated — with the paths of the exports, instead of the console's log.
/// </summary>
internal static class Msdial
{
    public static async Task<Document?> RunAsync(string command, Args args, TextWriter? diagnostics, bool quiet)
    {
        var input = args.Get("i") ?? args.Get("input") ?? throw new CliError(ErrorCode.BadArguments, $"{command} needs -i INPUT");
        var output = args.Get("o") ?? args.Get("output") ?? throw new CliError(ErrorCode.BadArguments, $"{command} needs -o OUTPUT");
        var method = args.Get("m") ?? args.Get("method") ?? throw new CliError(ErrorCode.BadArguments, $"{command} needs -m METHOD");
        input = Path.GetFullPath(input);
        output = Path.GetFullPath(output);
        method = Path.GetFullPath(method);
        if (!File.Exists(input) && !Directory.Exists(input)) throw new CliError(ErrorCode.Unreadable, $"{input} is not there");
        if (!File.Exists(method)) throw new CliError(ErrorCode.Unreadable, $"the method file {method} is not there");
        Directory.CreateDirectory(output);

        var overrides = new List<(string Key, string Value)>();
        if (args.Get("acquisition-type") is { } acq)
        {
            var a = acq.Trim().ToUpperInvariant();
            if (a is not ("DDA" or "SWATH" or "AIF")) throw new CliError(ErrorCode.BadArguments, "--acquisition-type is DDA, SWATH or AIF");
            overrides.Add(("Acquisition type", a));
        }
        if (args.Get("library") is { } lib)
        {
            var full = Path.GetFullPath(lib);
            if (!File.Exists(full)) throw new CliError(ErrorCode.Unreadable, $"the library {full} is not there");
            overrides.Add(("MSP file path", full));
        }
        var methodUsed = method;
        if (overrides.Count > 0)
        {
            methodUsed = Path.Combine(output, "milx_method.txt");
            File.WriteAllText(methodUsed, Override(File.ReadAllText(method), overrides), new UTF8Encoding(false));
        }

        var argv = new List<string> { command, "-i", input, "-o", output, "-m", methodUsed };
        if (args.Flag("p") || args.Flag("project")) argv.Add("-p");
        foreach (var name in args.Names.Where(n => n is not ("i" or "input" or "o" or "output" or "m" or "method" or "p" or "project" or "acquisition-type" or "library" or "summary" or "format" or "quiet" or "cache")))
        {
            foreach (var v in args.All(name)) { argv.Add((name.Length == 1 ? "-" : "--") + name); if (v != "true") argv.Add(v); }
        }

        var summary = args.Flag("summary");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // the console writes its log to stdout; with a JSON answer wanted, that log goes to a file beside the results
        var logPath = Path.Combine(output, "milx-run.log");
        int exit;
        var original = Console.Out;
        using (var log = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true })
        {
            Console.SetOut(summary || quiet ? log : new Tee(original, log));
            try { exit = await InvokeConsole(argv.ToArray()); }
            finally { Console.SetOut(original); }
        }
        if (!summary)
        {
            if (exit != 0) throw new CliError(ErrorCode.Internal, $"{command} exited with {exit}; see {logPath}", "msdial_failed");
            return null;
        }
        var doc = new Document(command, new JsonObject { ["input"] = input, ["output"] = output, ["method"] = method, ["method_used"] = methodUsed, ["overrides"] = new JsonObject(overrides.ToDictionary(o => o.Key, o => (JsonNode?)o.Value)) });
        doc.Root["exit"] = exit;
        doc.Root["ok"] = exit == 0;
        doc.Root["seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 1);
        doc.Root["log"] = logPath;
        Summarise(doc, output);
        if (exit != 0) throw new CliError(ErrorCode.Internal, $"{command} exited with {exit}; see {logPath}", "msdial_failed");
        return doc;
    }

    private static async Task<int> InvokeConsole(string[] argv)
    {
        var program = typeof(MainProcess).Assembly.GetType("CompMs.App.MsdialConsole.Program") ?? throw new CliError(ErrorCode.Internal, "the MS-DIAL console's entry point was not found");
        var main = program.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ?? throw new CliError(ErrorCode.Internal, "the MS-DIAL console has no Main");
        var result = main.Invoke(null, new object[] { argv });
        return result switch
        {
            Task<int> t => await t,
            int i => i,
            _ => 0,
        };
    }

    /// <summary>The method text with the given keys replaced (or appended when absent), case-insensitively on the key.</summary>
    internal static string Override(string methodText, IReadOnlyList<(string Key, string Value)> overrides)
    {
        var lines = methodText.Replace("\r\n", "\n").Split('\n').ToList();
        foreach (var (key, value) in overrides)
        {
            var found = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                if (string.Equals(lines[i][..colon].Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key}: {value}";
                    found = true;
                }
            }
            if (!found) lines.Add($"{key}: {value}");
        }
        return string.Join("\n", lines).TrimEnd('\n') + "\n";
    }

    /// <summary>Counts what the run wrote: peaks per file from the .mdpeak tables, features from the .mdalign, MS/MS assigned and annotated in both.</summary>
    internal static void Summarise(Document doc, string output)
    {
        var files = new JsonArray();
        long peaks = 0, withMsms = 0, annotated = 0;
        foreach (var path in Directory.EnumerateFiles(output, "*.mdpeak").OrderBy(f => f, StringComparer.Ordinal))
        {
            var (n, msms, named) = CountTable(path, "MSMS spectrum", "Name");
            peaks += n; withMsms += msms; annotated += named;
            files.Add(new JsonObject { ["file"] = Path.GetFileNameWithoutExtension(path), ["peaks"] = n, ["msms_assigned"] = msms, ["annotated"] = named, ["path"] = path });
        }
        doc.Root["files"] = files;
        doc.Root["peaks"] = new JsonObject { ["total"] = peaks, ["msms_assigned"] = withMsms, ["annotated"] = annotated };
        var align = Directory.EnumerateFiles(output, "*.mdalign").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (align is not null)
        {
            var (n, msms, named) = CountTable(align, "MS/MS assigned", "Metabolite name");
            doc.Root["alignment"] = new JsonObject { ["features"] = n, ["msms_assigned"] = msms, ["annotated"] = named, ["path"] = align };
        }
        else doc.Root["alignment"] = null;
        var exports = new JsonObject();
        foreach (var f in Directory.EnumerateFiles(output).OrderBy(f => f, StringComparer.Ordinal))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var key = ext switch { ".mdpeak" => "peak_tables", ".mdmsp" => "msms_spectra", ".mdalign" => "alignment_table", ".mztab" => "mztab", ".mdproject" => "project", ".tsv" when f.EndsWith(".qa.tsv", StringComparison.OrdinalIgnoreCase) => "qa_matrix", _ => null };
            if (key is null) continue;
            if (exports[key] is JsonArray arr) arr.Add(f);
            else exports[key] = new JsonArray(f);
        }
        doc.Root["exports"] = exports;
    }

    private static (long Rows, long WithMsms, long Annotated) CountTable(string path, string msmsColumn, string nameColumn)
    {
        long rows = 0, msms = 0, named = 0;
        int msmsIndex = -1, nameIndex = -1;
        var inRows = false;
        foreach (var line in File.ReadLines(path))
        {
            if (!inRows)
            {
                var cells = line.Split('\t');
                var idx = Array.FindIndex(cells, c => c == nameColumn);
                if (idx >= 0)
                {
                    nameIndex = idx;
                    msmsIndex = Array.FindIndex(cells, c => c == msmsColumn);
                    inRows = true;
                }
                continue;
            }
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            rows++;
            if (msmsIndex >= 0 && msmsIndex < parts.Length)
            {
                var v = parts[msmsIndex];
                if (v.Length > 0 && v != "null" && !v.Equals("False", StringComparison.OrdinalIgnoreCase)) msms++;
            }
            if (nameIndex < parts.Length)
            {
                var name = parts[nameIndex];
                if (name.Length > 0 && !name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase) && name != "null") named++;
            }
        }
        return (rows, msms, named);
    }

    /// <summary>Writes to the screen and to the log at once, for a run watched by a person.</summary>
    private sealed class Tee : TextWriter
    {
        private readonly TextWriter _a, _b;
        public Tee(TextWriter a, TextWriter b) { _a = a; _b = b; }
        public override Encoding Encoding => _a.Encoding;
        public override void Write(char value) { _a.Write(value); _b.Write(value); }
        public override void Write(string? value) { _a.Write(value); _b.Write(value); }
        public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
        public override void Flush() { _a.Flush(); _b.Flush(); }
    }
}
