using System.Globalization;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Tools.ResultCompare;

internal static class Program
{
    private static async Task<int> Main(string[] args) {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 2 && args[0] == "--dump-params") {
            var project = await ProjectOpener.OpenAsync(args[1]);
            DumpParameters(project);
            return 0;
        }
        if (args.Length < 2) {
            Console.Error.WriteLine("usage: ResultCompare <reference .mdproject|folder> <test .mdproject|folder> [rtTol] [mzTol] [out.tsv]");
            return 1;
        }
        var rtTol = args.Length > 2 && double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0.1;
        var mzTol = args.Length > 3 && double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 0.01;
        var outPath = args.Length > 4 ? args[4] : null;

        var reference = await Load(args[0], "REFERENCE");
        var test = await Load(args[1], "TEST");

        Console.WriteLine();
        Console.WriteLine("=== peaks per sample ===");
        Console.WriteLine($"{"sample",-34}{"ref",10}{"test",10}{"ref ann",10}{"test ann",10}");
        var refPeaks = await PeakCounts(reference);
        var testPeaks = await PeakCounts(test);
        foreach (var name in refPeaks.Keys.Union(testPeaks.Keys).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
            refPeaks.TryGetValue(name, out var a);
            testPeaks.TryGetValue(name, out var b);
            Console.WriteLine($"{name,-34}{a.Total,10}{b.Total,10}{a.Annotated,10}{b.Annotated,10}");
        }
        Console.WriteLine($"{"TOTAL",-34}{refPeaks.Values.Sum(v => v.Total),10}{testPeaks.Values.Sum(v => v.Total),10}{refPeaks.Values.Sum(v => v.Annotated),10}{testPeaks.Values.Sum(v => v.Annotated),10}");

        Console.WriteLine();
        Console.WriteLine("=== peak agreement on the first sample ===");
        await ComparePeaks(reference, test, rtTol, mzTol, outPath is null ? null : Path.ChangeExtension(outPath, ".peaks.tsv"));

        Console.WriteLine();
        Console.WriteLine("=== aligned features ===");
        var refTable = reference.AlignmentFile is null ? null : await ResultLoader.LoadAlignmentTableAsync(reference.AlignmentFile, reference.AnalysisFiles);
        var testTable = test.AlignmentFile is null ? null : await ResultLoader.LoadAlignmentTableAsync(test.AlignmentFile, test.AnalysisFiles);
        if (refTable is null || testTable is null) {
            Console.WriteLine("one of the datasets has no alignment result");
            return 0;
        }
        Console.WriteLine($"reference: {refTable.Spots.Count} spots ({refTable.Spots.Count(s => s.IsAnnotated)} annotated)");
        Console.WriteLine($"test     : {testTable.Spots.Count} spots ({testTable.Spots.Count(s => s.IsAnnotated)} annotated)");

        // greedy nearest match in (RT, m/z), each test spot used once
        var used = new bool[testTable.Spots.Count];
        var rows = new List<string>();
        int matched = 0, sameName = 0, refAnnMatched = 0;
        foreach (var a in refTable.Spots.OrderBy(s => s.Mz)) {
            var best = -1;
            var bestCost = double.MaxValue;
            for (var i = 0; i < testTable.Spots.Count; i++) {
                if (used[i]) continue;
                var b = testTable.Spots[i];
                var dmz = Math.Abs(a.Mz - b.Mz);
                var drt = Math.Abs(a.Rt - b.Rt);
                if (dmz > mzTol || drt > rtTol) continue;
                var cost = dmz / mzTol + drt / rtTol;
                if (cost < bestCost) { bestCost = cost; best = i; }
            }
            if (best < 0) {
                rows.Add($"reference-only\t{a.Id}\t{a.Rt:F3}\t{a.Mz:F5}\t{a.Name}\t\t\t\t");
                continue;
            }
            used[best] = true;
            matched++;
            var t = testTable.Spots[best];
            var agree = string.Equals(Norm(a.Name), Norm(t.Name), StringComparison.OrdinalIgnoreCase);
            if (agree) sameName++;
            if (a.IsAnnotated && t.IsAnnotated) refAnnMatched++;
            rows.Add($"matched\t{a.Id}\t{a.Rt:F3}\t{a.Mz:F5}\t{a.Name}\t{t.Id}\t{t.Rt:F3}\t{t.Mz:F5}\t{t.Name}\t{(agree ? "same" : "different")}");
        }
        for (var i = 0; i < testTable.Spots.Count; i++) {
            if (used[i]) continue;
            var t = testTable.Spots[i];
            rows.Add($"test-only\t\t\t\t\t{t.Id}\t{t.Rt:F3}\t{t.Mz:F5}\t{t.Name}\t");
        }

        var refAnn = refTable.Spots.Count(s => s.IsAnnotated);
        Console.WriteLine();
        Console.WriteLine($"matched within {rtTol:F2} min / {mzTol:F3} Da : {matched}");
        Console.WriteLine($"  of the reference spots        : {Pct(matched, refTable.Spots.Count)}");
        Console.WriteLine($"  of the test spots             : {Pct(matched, testTable.Spots.Count)}");
        Console.WriteLine($"reference-only                  : {refTable.Spots.Count - matched}");
        Console.WriteLine($"test-only                       : {testTable.Spots.Count - matched}");
        Console.WriteLine($"same name on matched pairs      : {sameName}");
        Console.WriteLine($"reference annotated spots       : {refAnn}, of those matched and also annotated: {refAnnMatched}");

        if (outPath is not null) {
            await File.WriteAllLinesAsync(outPath,
                new[] { "kind\tref_id\tref_rt\tref_mz\tref_name\ttest_id\ttest_rt\ttest_mz\ttest_name\tname" }.Concat(rows));
            Console.WriteLine($"\nper-feature detail written to {outPath}");
        }
        return 0;
    }


    /// <summary>
    /// Matches the peak lists of the first sample the two datasets share, and describes what is
    /// missing: heights of the reference-only peaks, and the height ratio of the matched ones.
    /// </summary>
    private static async Task ComparePeaks(OpenedProject reference, OpenedProject test, double rtTol, double mzTol, string? outPath) {
        var a = reference.AnalysisFiles.FirstOrDefault();
        var b = test.AnalysisFiles.FirstOrDefault(f => string.Equals(f.AnalysisFileName, a?.AnalysisFileName, StringComparison.OrdinalIgnoreCase))
                ?? test.AnalysisFiles.FirstOrDefault();
        if (a is null || b is null) return;
        var refPeaks = (await ResultLoader.LoadPeakTableAsync(a)).OrderBy(p => p.Mz).ToList();
        var testPeaks = (await ResultLoader.LoadPeakTableAsync(b)).OrderBy(p => p.Mz).ToList();
        Console.WriteLine($"sample {a.AnalysisFileName}: reference {refPeaks.Count}, test {testPeaks.Count}");

        var used = new bool[testPeaks.Count];
        var rows = new List<string>();
        var ratios = new List<double>();
        var missedHeights = new List<double>();
        var missedWithMs2 = 0;
        var matched = 0;
        foreach (var p in refPeaks) {
            var best = -1;
            var bestCost = double.MaxValue;
            for (var i = 0; i < testPeaks.Count; i++) {
                if (used[i]) continue;
                var q = testPeaks[i];
                var dmz = Math.Abs(p.Mz - q.Mz);
                if (dmz > mzTol) continue;
                var drt = Math.Abs(p.Rt - q.Rt);
                if (drt > rtTol) continue;
                var cost = dmz / mzTol + drt / rtTol;
                if (cost < bestCost) { bestCost = cost; best = i; }
            }
            if (best < 0) {
                missedHeights.Add(p.Height);
                if (p.HasMs2) missedWithMs2++;
                rows.Add($"reference-only\t{p.Mz:F5}\t{p.Rt:F3}\t{p.Height:F0}\t{p.HasMs2}\t{p.Name}\t\t\t\t");
                continue;
            }
            used[best] = true;
            matched++;
            var t2 = testPeaks[best];
            if (p.Height > 0) ratios.Add(t2.Height / p.Height);
            rows.Add($"matched\t{p.Mz:F5}\t{p.Rt:F3}\t{p.Height:F0}\t{p.HasMs2}\t{p.Name}\t{t2.Mz:F5}\t{t2.Rt:F3}\t{t2.Height:F0}\t{t2.Name}\t{t2.HasMs2}");
        }
        for (var i = 0; i < testPeaks.Count; i++) {
            if (used[i]) continue;
            var q = testPeaks[i];
            rows.Add($"test-only\t\t\t\t\t\t{q.Mz:F5}\t{q.Rt:F3}\t{q.Height:F0}\t{q.Name}");
        }
        Console.WriteLine($"  matched {matched}, reference-only {refPeaks.Count - matched}, test-only {testPeaks.Count - matched}");
        if (missedHeights.Count > 0) {
            missedHeights.Sort();
            Console.WriteLine($"  reference-only heights: min {missedHeights[0]:F0}, median {missedHeights[missedHeights.Count / 2]:F0}, max {missedHeights[^1]:F0}; with MS2: {missedWithMs2}");
        }
        if (ratios.Count > 0) {
            ratios.Sort();
            Console.WriteLine($"  matched height ratio test/reference: median {ratios[ratios.Count / 2]:F3}, 10th {ratios[ratios.Count / 10]:F3}, 90th {ratios[ratios.Count * 9 / 10]:F3}");
        }
        // For reference peaks whose MS2 we did not link: how close is the nearest precursor we recorded?
        // MS-DIAL links a DDA product spectrum to a peak when |precursor - peak m/z| < CentroidMs2Tolerance.
        var lost = refPeaks.Where(p => p.HasMs2).Zip(Enumerable.Repeat(0, refPeaks.Count)).Select(x => x.First).ToList();
        try {
            var raw = await ResultLoader.LoadRawMeasurementAsync(b);
            var ms2 = raw.SpectrumList.Where(s => s.MsLevel > 1 && s.Precursor is not null && s.Spectrum.Length > 0)
                                      .Select(s => (Mz: s.Precursor!.SelectedIonMz, Rt: s.ScanStartTime)).OrderBy(x => x.Mz).ToList();
            Console.WriteLine($"  raw product spectra available: {ms2.Count}");
            var deltas = new List<double>();
            var none = 0;
            foreach (var row in rows.Where(r => r.StartsWith("matched"))) {
                var f = row.Split('\t');
                if (f[4] != "True" || f.Length < 11 || f[10] == "True") continue;
                var mz = double.Parse(f[1], CultureInfo.InvariantCulture);
                var rt = double.Parse(f[2], CultureInfo.InvariantCulture);
                var best = double.MaxValue;
                foreach (var s in ms2) {
                    if (s.Mz < mz - 0.5) continue;
                    if (s.Mz > mz + 0.5) break;
                    if (Math.Abs(s.Rt - rt) > 0.3) continue;
                    best = Math.Min(best, Math.Abs(s.Mz - mz));
                }
                if (best == double.MaxValue) none++; else deltas.Add(best);
            }
            deltas.Sort();
            Console.WriteLine($"  peaks whose MS2 we lost: {deltas.Count + none}; no product spectrum within 0.5 Da / 0.3 min at all: {none}");
            if (deltas.Count > 0) {
                Console.WriteLine($"  nearest recorded precursor offset: median {deltas[deltas.Count / 2]:F5}, p90 {deltas[deltas.Count * 9 / 10]:F5}, within 0.01: {deltas.Count(d => d < 0.01)}");
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"  (raw file not readable for the precursor check: {ex.Message})");
        }

        var refMs2 = refPeaks.Count(p => p.HasMs2);
        var testMs2 = testPeaks.Count(p => p.HasMs2);
        Console.WriteLine($"  peaks carrying MS2: reference {refMs2}, test {testMs2}");
        if (outPath is not null) {
            await File.WriteAllLinesAsync(outPath, new[] { "kind\tref_mz\tref_rt\tref_height\tref_ms2\tref_name\ttest_mz\ttest_rt\ttest_height\ttest_name\ttest_ms2" }.Concat(rows));
            Console.WriteLine($"  per-peak detail written to {outPath}");
        }
    }


    /// <summary>
    /// Prints the annotation settings a saved project actually ran with. MS-DIAL's parameter export
    /// leaves the library search cut-offs out, so the project file is the only place to read them.
    /// </summary>
    private static void DumpParameters(OpenedProject p) {
        var param = p.Parameter;
        if (param is null) { Console.WriteLine("no parameters in this project"); return; }
        Console.WriteLine($"MS-DIAL version   : {param.ProjectParam.MsdialVersionNumber}");
        Console.WriteLine($"TargetOmics       : {param.TargetOmics}");
        Console.WriteLine($"MspFilePath       : '{param.MspFilePath}'");
        Console.WriteLine($"LbmFilePath       : '{param.LbmFilePath}'");
        Console.WriteLine($"TextDBFilePath    : '{param.TextDBFilePath}'");
        Console.WriteLine($"OnlyReportTopHit  : {param.OnlyReportTopHitInMspSearch}");
        Console.WriteLine($"acquisition type  : {string.Join(", ", p.AnalysisFiles.Select(f => f.AcquisitionType.ToString()).Distinct())}");
        foreach (var (label, search) in new[] { ("MspSearchParam", param.MspSearchParam), ("LbmSearchParam", param.LbmSearchParam), ("TextDbSearchParam", param.TextDbSearchParam) }) {
            if (search is null) continue;
            Console.WriteLine($"--- {label} ---");
            foreach (var pi in search.GetType().GetProperties().OrderBy(x => x.Name)) {
                object? v;
                try { v = pi.GetValue(search); } catch { continue; }
                if (v is null) continue;
                var ty = v.GetType();
                if (!(ty.IsPrimitive || ty.IsEnum || v is string or decimal)) continue;
                Console.WriteLine($"  {pi.Name} = {Convert.ToString(v, CultureInfo.InvariantCulture)}");
            }
        }
    }

    private static string Norm(string name) => name.Replace("Unknown", string.Empty).Trim();

    private static string Pct(int n, int total) => total == 0 ? "n/a" : $"{n}/{total} ({100.0 * n / total:F1} %)";

    private static async Task<OpenedProject> Load(string path, string label) {
        var p = await ProjectOpener.OpenAsync(path);
        Console.WriteLine($"{label}: {path}");
        Console.WriteLine($"  source={p.Source}  files={p.AnalysisFiles.Count}  alignment={(p.AlignmentFile is null ? "none" : Path.GetFileName(p.AlignmentFile.FilePath))}");
        Console.WriteLine("  acquisition type per file: " + string.Join(", ", p.AnalysisFiles.Select(f => f.AcquisitionType.ToString()).Distinct()));
        if (p.Parameter is not null) {
            Console.WriteLine($"  version={p.Parameter.ProjectParam.MsdialVersionNumber}  minHeight={p.Parameter.MinimumAmplitude}  massSlice={p.Parameter.MassSliceWidth}  sigma={p.Parameter.SigmaWindowValue}");
        }
        return p;
    }

    private static async Task<Dictionary<string, (int Total, int Annotated)>> PeakCounts(OpenedProject p) {
        var result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in p.AnalysisFiles) {
            try {
                var peaks = await ResultLoader.LoadPeakTableAsync(f);
                result[f.AnalysisFileName] = (peaks.Count, peaks.Count(x => x.IsAnnotated));
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"  ! {f.AnalysisFileName}: {ex.Message}");
            }
        }
        return result;
    }
}
