using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Clearcore2.Data;
using Clearcore2.Data.AnalystDataProvider;
using Clearcore2.Data.DataAccess.SampleData;
using Clearcore2.Utility;

namespace OpenDIAL.Tools.WiffProbe;

internal static class Program
{
    private static int Main(string[] args) {
        // The Clearcore2 assemblies sit next to the executable but are not in deps.json (they are
        // referenced with Private=false so the licensed SDK is never redistributed by the build).
        AssemblyLoadContext.Default.Resolving += (ctx, name) => {
            var dll = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(dll) ? ctx.LoadFromAssemblyPath(dll) : null;
        };
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 0) { Console.Error.WriteLine("usage: WiffProbe <file.wiff> [cycles]"); return 1; }
        return Probe(Path.GetFullPath(args[0]), args.Length > 1 && int.TryParse(args[1], out var c) ? c : 6);
    }


    /// <summary>Counts what the dependent (IDA MS2) experiments actually hold, to see how many
    /// product spectra a reader should emit and why some would be dropped.</summary>

    /// <summary>Lists the SDK surface that could centroid a profile spectrum the way SCIEX does.</summary>

    /// <summary>
    /// Compares the two ways of turning one profile survey spectrum into centroids: SCIEX's own
    /// (MassSpectrum.GetPeakArray, what Analyst and the closed MS-DIAL reader use) and MS-DIAL's
    /// generic local-maximum method, which is what this plugin has been applying.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CompareCentroiding(string path) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var d0 = Path.GetDirectoryName(typeof(AnalystDataProviderFactory).Assembly.Location)!;
            Assembly.LoadFrom(Path.Combine(d0, "Clearcore2.StructuredStorage.dll"))
                .GetType("Clearcore2.StructuredStorage.StgStorage", true)!
                .GetField("sWindows", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
        }
        var provider = new AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared);
        try {
            var ms = AnalystDataProviderFactory.CreateBatch(path, provider).GetSample(0).MassSpectrometerSample;
            var expIndex = int.TryParse(Environment.GetEnvironmentVariable("WIFFPROBE_EXPERIMENT"), out var ei) ? ei : 0;
            var exp = ms.GetMSExperiment(expIndex);
            var cycles = new[] { 60, 120, 240, 360 };
            foreach (var cycle in cycles) {
                if (cycle >= exp.Details.NumberOfScans) continue;
                var spectrum = exp.GetMassSpectrum(cycle);
                var profilePoints = spectrum.NumDataPoints;
                if (profilePoints == 0) { Console.WriteLine($"cycle {cycle}: not triggered"); continue; }

                // SCIEX centroids
                var sciex = exp.GetPeakArray(cycle).Cast<object>().ToArray();

                // our route: AddZeros then MS-DIAL's local maximum method
                var withZeros = exp.GetMassSpectrum(cycle);
                try { exp.AddZeros(withZeros, 1); } catch { }
                var xs = withZeros.GetActualXValues();
                var ys = withZeros.GetActualYValues();
                var raw = new CompMs.Common.DataObj.RawPeakElement[xs.Length];
                for (var i = 0; i < xs.Length; i++) { raw[i].Mz = xs[i]; raw[i].Intensity = ys[i]; }
                var ours = CompMs.Common.Algorithm.PeakPick.SpectralCentroiding.CentroidByLocalMaximumMethod(raw, absThreshold: 0.0);

                if (cycle == cycles[0] && sciex.Length > 0) {
                    Console.WriteLine("PeakClass members: " + string.Join(", ",
                        sciex[0].GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name + "=" + f.GetValue(sciex[0]))));
                }
                Console.WriteLine($"cycle {cycle}: profile points {profilePoints}, SCIEX peaks {sciex.Length}, local-maximum centroids {ours.Length}");
                // match the two lists and report the intensity ratio
                var ratios = new List<double>();
                foreach (var p in sciex.OrderByDescending(p => PeakY(p)).Take(400)) {
                    var x = PeakX(p);
                    var y = PeakY(p);
                    if (y <= 0) continue;
                    var best = -1.0;
                    var bestD = double.MaxValue;
                    foreach (var o in ours) {
                        var dd = Math.Abs(o.Mz - x);
                        if (dd < bestD) { bestD = dd; best = o.Intensity; }
                    }
                    if (bestD < 0.01 && best > 0) ratios.Add(best / y);
                }
                if (ratios.Count > 0) {
                    ratios.Sort();
                    Console.WriteLine($"   intensity ratio ours/SCIEX over {ratios.Count} matched peaks: median {ratios[ratios.Count / 2]:F4}, p10 {ratios[ratios.Count / 10]:F4}, p90 {ratios[ratios.Count * 9 / 10]:F4}");
                }
                if (cycle == cycles[0]) {
                    Console.WriteLine("   strongest SCIEX peaks (x, y) and the nearest local-maximum centroid:");
                    foreach (var p in sciex.OrderByDescending(PeakY).Take(5)) {
                        var x = PeakX(p);
                        var near = ours.OrderBy(o => Math.Abs(o.Mz - x)).FirstOrDefault();
                        Console.WriteLine($"     {x,12:F5} {PeakY(p),12:F1}   ->  {near.Mz,12:F5} {near.Intensity,12:F1}");
                    }
                }
            }
            return 0;
        }
        finally { try { provider.Close(); } catch { } }
    }

    private static double PeakX(object p) => Convert.ToDouble(p.GetType().GetField("apexX")?.GetValue(p) ?? p.GetType().GetProperty("apexX")?.GetValue(p) ?? 0.0, CultureInfo.InvariantCulture);
    private static double PeakY(object p) => Convert.ToDouble(p.GetType().GetField("apexY")?.GetValue(p) ?? p.GetType().GetProperty("apexY")?.GetValue(p) ?? 0.0, CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DumpApi() {
        Console.WriteLine("--- MassSpectrum members ---");
        foreach (var mi in typeof(MassSpectrum).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).OrderBy(x => x.Name)) {
            Console.WriteLine($"  {mi.MemberType,-8} {mi}");
        }
        Console.WriteLine();
        Console.WriteLine("--- MSExperiment members ---");
        foreach (var mi in typeof(MSExperiment).GetMembers(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name)) {
            Console.WriteLine($"  {mi.MemberType,-8} {mi}");
        }
        Console.WriteLine();
        var dir = Path.GetDirectoryName(typeof(AnalystDataProviderFactory).Assembly.Location)!;
        foreach (var name in new[] { "Clearcore2.RawXYProcessing", "Clearcore2.InternalRawXYProcessing", "Sciex.Data.XYData", "Clearcore2.Data.CommonInterfaces" }) {
            var file = Path.Combine(dir, name + ".dll");
            if (!File.Exists(file)) continue;
            Console.WriteLine($"--- public types of {name} mentioning peak/centroid ---");
            foreach (var ty in Assembly.LoadFrom(file).GetExportedTypes()) {
                if (ty.Name.IndexOf("peak", StringComparison.OrdinalIgnoreCase) < 0
                    && ty.Name.IndexOf("centroid", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Console.WriteLine($"  {ty.FullName}");
                foreach (var mi in ty.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                    Console.WriteLine($"      {mi}");
                }
            }
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CountDependentScans(string path) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var dir = Path.GetDirectoryName(typeof(AnalystDataProviderFactory).Assembly.Location)!;
            var a = Assembly.LoadFrom(Path.Combine(dir, "Clearcore2.StructuredStorage.dll"));
            a.GetType("Clearcore2.StructuredStorage.StgStorage", true)!
             .GetField("sWindows", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
        }
        var provider = new AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared);
        try {
            var ms = AnalystDataProviderFactory.CreateBatch(path, provider).GetSample(0).MassSpectrometerSample;
            long total = 0, withData = 0, withParent = 0, dataNoParent = 0, parentNoData = 0;
            for (var e = 1; e < ms.ExperimentCount; e++) {
                var exp = ms.GetMSExperiment(e);
                for (var cycle = 0; cycle < exp.Details.NumberOfScans; cycle++) {
                    total++;
                    int n;
                    double parent;
                    try { n = exp.GetMassSpectrum(cycle).NumDataPoints; parent = exp.GetMassSpectrumInfo(cycle).ParentMZ; }
                    catch { continue; }
                    if (n > 0) withData++;
                    if (parent > 0) withParent++;
                    if (n > 0 && parent <= 0) dataNoParent++;
                    if (n <= 0 && parent > 0) parentNoData++;
                }
            }
            Console.WriteLine($"dependent scan slots      : {total}");
            Console.WriteLine($"  with data points        : {withData}");
            Console.WriteLine($"  with a precursor        : {withParent}");
            Console.WriteLine($"  data but no precursor   : {dataNoParent}");
            Console.WriteLine($"  precursor but no data   : {parentNoData}");
            return 0;
        }
        finally { try { provider.Close(); } catch { } }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Probe(string path, int cyclesToPrint) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var dir = Path.GetDirectoryName(typeof(AnalystDataProviderFactory).Assembly.Location)!;
            var asm = Assembly.LoadFrom(Path.Combine(dir, "Clearcore2.StructuredStorage.dll"));
            asm.GetType("Clearcore2.StructuredStorage.StgStorage", true)!
               .GetField("sWindows", BindingFlags.NonPublic | BindingFlags.Static)!
               .SetValue(null, false);
        }

        if (Environment.GetEnvironmentVariable("WIFFPROBE_COUNT") == "1") return CountDependentScans(path);
        if (Environment.GetEnvironmentVariable("WIFFPROBE_API") == "1") return DumpApi();
        if (Environment.GetEnvironmentVariable("WIFFPROBE_CENTROID") == "1") return CompareCentroiding(path);
        var provider = new AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared);
        try {
            var batch = AnalystDataProviderFactory.CreateBatch(path, provider);
            var names = batch.GetSampleNames();
            Console.WriteLine($"file      : {path}");
            Console.WriteLine($"samples   : {names.Length} [{string.Join(", ", names)}]");
            var ms = batch.GetSample(0).MassSpectrometerSample;
            Console.WriteLine($"instrument: {ms.InstrumentName}");
            Console.WriteLine($"HasIDAData: {ms.HasIDAData}");
            Console.WriteLine($"experiments: {ms.ExperimentCount}");
            Console.WriteLine();

            // How the cycle clock runs across experiments: MS-DIAL brackets the MS2 of a cycle
            // between consecutive survey scans, so the dependent scans must fall inside their cycle.
            Console.WriteLine("retention time (min) per cycle, by experiment");
            Console.Write($"{"cycle",6}");
            for (var e = 0; e < Math.Min(ms.ExperimentCount, 6); e++) Console.Write($"{"exp" + e,12}");
            Console.WriteLine($"{"exp" + (ms.ExperimentCount - 1),12}");
            for (var cycle = 0; cycle < 4; cycle++) {
                Console.Write($"{cycle,6}");
                for (var e = 0; e < Math.Min(ms.ExperimentCount, 6); e++) Console.Write($"{ms.GetMSExperiment(e).GetRTFromExperimentCycle(cycle),12:F5}");
                Console.WriteLine($"{ms.GetMSExperiment(ms.ExperimentCount - 1).GetRTFromExperimentCycle(cycle),12:F5}");
            }
            Console.WriteLine();

            for (var e = 0; e < ms.ExperimentCount; e++) {
                var exp = ms.GetMSExperiment(e);
                var d = exp.Details;
                Console.WriteLine($"--- experiment {e} ---");
                Console.WriteLine($"  ExperimentType={d.ExperimentType}  IsSwath={d.IsSwath}  Polarity={d.Polarity}  scans={d.NumberOfScans}  mass {d.StartMass}-{d.EndMass}");
                foreach (var p in d.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name)) {
                    if (p.GetIndexParameters().Length > 0) continue;
                    object? v;
                    try { v = p.GetValue(d); } catch { continue; }
                    if (v is null or Array) continue;
                    var t = v.GetType();
                    if (!(t.IsPrimitive || t.IsEnum || v is string or decimal)) continue;
                    Console.WriteLine($"    {p.Name} = {Convert.ToString(v, CultureInfo.InvariantCulture)}");
                }
                if (d.MassRangeInfo is { Length: > 0 }) {
                    for (var i = 0; i < d.MassRangeInfo.Length; i++) {
                        var mr = d.MassRangeInfo[i];
                        Console.Write($"    MassRangeInfo[{i}] {mr.GetType().Name}");
                        if (mr is FragmentBasedScanMassRange fr) {
                            Console.Write($" FixedMasses=[{string.Join(", ", fr.FixedMasses ?? Array.Empty<double>())}] IsolationWindow={fr.IsolationWindow.ToString(CultureInfo.InvariantCulture)}");
                        }
                        Console.WriteLine();
                    }
                }
                // full record of one triggered dependent scan: everything SCIEX stores about the precursor
                if (e == 1) {
                    for (var cycle = 0; cycle < d.NumberOfScans; cycle++) {
                        MassSpectrumInfo probe;
                        try { probe = exp.GetMassSpectrumInfo(cycle); } catch { continue; }
                        if (probe.ParentMZ <= 0 || exp.GetMassSpectrum(cycle).NumDataPoints == 0) continue;
                        Console.WriteLine($"    [MassSpectrumInfo of cycle {cycle}]");
                        foreach (var pr in probe.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name)) {
                            if (pr.GetIndexParameters().Length > 0) continue;
                            object? v;
                            try { v = pr.GetValue(probe); } catch { continue; }
                            if (v is null) continue;
                            var ty = v.GetType();
                            if (!(ty.IsPrimitive || ty.IsEnum || v is string or decimal)) continue;
                            Console.WriteLine($"      {pr.Name} = {Convert.ToString(v, CultureInfo.InvariantCulture)}");
                        }
                        break;
                    }
                }
                Console.WriteLine("    cycle  MSLevel    ParentMZ     CE  centroid  points   RT(min)");
                var printed = 0;
                for (var cycle = 0; cycle < d.NumberOfScans && printed < cyclesToPrint; cycle++) {
                    try {
                        var info = exp.GetMassSpectrumInfo(cycle);
                        var spec = exp.GetMassSpectrum(cycle);
                        Console.WriteLine($"    {cycle,5}  {info.MSLevel,7}  {info.ParentMZ,10:F4}  {info.CollisionEnergy,5:F1}  {info.CentroidMode,8}  {spec.NumDataPoints,6}  {exp.GetRTFromExperimentCycle(cycle),8:F4}");
                    }
                    catch (Exception ex) { Console.WriteLine($"    {cycle,5}  <{ex.GetType().Name}: {ex.Message}>"); }
                    printed++;
                }
                if (e > 0) {
                    var distinct = new HashSet<double>();
                    var empty = 0;
                    for (var cycle = 0; cycle < d.NumberOfScans; cycle++) {
                        try {
                            if (exp.GetMassSpectrum(cycle).NumDataPoints == 0) { empty++; continue; }
                            distinct.Add(Math.Round(exp.GetMassSpectrumInfo(cycle).ParentMZ, 4));
                        }
                        catch { }
                    }
                    Console.WriteLine($"    distinct ParentMZ over {d.NumberOfScans} cycles: {distinct.Count} (empty scans: {empty})");
                    if (distinct.Count is > 0 and <= 8) Console.WriteLine($"      values: {string.Join(", ", distinct.OrderBy(x => x).Select(x => x.ToString("F4", CultureInfo.InvariantCulture)))}");
                }
                Console.WriteLine();
            }
        }
        finally {
            try { provider.Close(); } catch { }
        }
        return 0;
    }
}
