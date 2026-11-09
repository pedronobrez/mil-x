using System.Globalization;
using System.Text.Json.Nodes;
using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>What the commands share: the file argument, the sample, the source, the envelope's input block.</summary>
internal static class Common
{
    public static string FileArgument(Args args, string command)
    {
        var file = args.Get("file") ?? args.Positional.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(file)) throw new CliError(ErrorCode.BadArguments, $"{command} needs a file: milx {command} FILE …");
        return Path.GetFullPath(file);
    }

    public static OpenFile OpenTarget(Args args, RawStore store, string command, bool headersOnly, CancellationToken ct)
    {
        var path = FileArgument(args, command);
        var sample = RawStore.ResolveSample(path, args.Get("sample"));
        return store.Open(path, sample, headersOnly, ct);
    }

    public static JsonObject Input(OpenFile file, Args? args = null)
    {
        var input = new JsonObject
        {
            ["file"] = file.Path,
            ["sample"] = file.Sample,
            ["sample_name"] = file.SampleName,
        };
        if (args is not null)
        {
            foreach (var name in args.Names.Where(n => n is not ("file" or "sample" or "format" or "quiet" or "cache")).OrderBy(n => n, StringComparer.Ordinal))
            {
                var all = args.All(name);
                input[name] = all.Count == 1 ? all[0] : new JsonArray(all.Select(v => (JsonNode)v).ToArray());
            }
        }
        return input;
    }

    /// <summary>The source named by --source (default ms1) and the scans it stands for.</summary>
    public static (XicSource Source, IReadOnlyList<int> Indices) Source(Args args, OpenFile file, string flag = "source")
    {
        XicSource source;
        try { source = XicSource.Parse(args.Get(flag, "ms1")); }
        catch (FormatException e) { throw new CliError(ErrorCode.BadArguments, e.Message); }
        return (source, Resolve(source, file));
    }

    public static IReadOnlyList<int> Resolve(XicSource source, OpenFile file)
    {
        try { return XicSources.Resolve(file.Raw, file.Channels, source); }
        catch (ArgumentOutOfRangeException e) { throw new CliError(ErrorCode.BadArguments, e.Message); }
    }

    /// <summary>The scans of a source inside an --rt A:B window, when one is given.</summary>
    public static IReadOnlyList<int> InRange(OpenFile file, IReadOnlyList<int> indices, (double Lo, double Hi)? rt)
    {
        if (rt is null) return indices;
        var (lo, hi) = rt.Value;
        return indices.Where(i => { var t = RawExplorer.RtMinutes(file.Raw.SpectrumList[i]); return t >= lo && t <= hi; }).ToList();
    }

    public static string KindName(RawChannelKind kind, RawChannel channel, RawMeasurement raw)
    {
        switch (kind)
        {
            case RawChannelKind.Ms1: return "ms1";
            case RawChannelKind.Ms2Events: return RawExplorer.HasExperiments(raw) ? "ida-slot" : "dda-events";
            default:
                // a window wider than a couple of daltons is SWATH; a narrow fixed precursor is a product-ion scan
                var first = raw.SpectrumList[channel.SpectrumIndices[0]];
                var width = (first.Precursor?.IsolationWindowLowerOffset ?? 0) + (first.Precursor?.IsolationWindowUpperOffset ?? 0);
                return width > 2.5 ? "swath" : "product";
        }
    }

    public static string Polarity(RawMeasurement raw, IReadOnlyList<int> indices)
    {
        if (indices.Count == 0) return "unknown";
        return raw.SpectrumList[indices[0]].ScanPolarity switch
        {
            ScanPolarity.Positive => "positive",
            ScanPolarity.Negative => "negative",
            _ => "unknown",
        };
    }

    public static (double Lo, double Hi) MassRange(RawMeasurement raw, IReadOnlyList<int> indices)
    {
        if (indices.Count == 0) return (0, 0);
        var first = raw.SpectrumList[indices[0]];
        if (first.ScanWindowUpperLimit > first.ScanWindowLowerLimit && first.ScanWindowUpperLimit > 0) return (first.ScanWindowLowerLimit, first.ScanWindowUpperLimit);
        double lo = double.MaxValue, hi = 0;
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            if (s.LowestObservedMz > 0 && s.LowestObservedMz < lo) lo = s.LowestObservedMz;
            if (s.HighestObservedMz > hi) hi = s.HighestObservedMz;
        }
        return lo == double.MaxValue ? (0, 0) : (lo, hi);
    }

    public static double CollisionEnergy(RawSpectrum s) => s.CollisionEnergy != 0 ? s.CollisionEnergy : (s.Precursor?.CollisionEnergy ?? 0);

    /// <summary>One scan as the contract's row.</summary>
    public static JsonObject ScanRow(RawMeasurement raw, int index, int positionInSource)
    {
        var s = raw.SpectrumList[index];
        var row = new JsonObject
        {
            ["scan"] = positionInSource,
            ["index"] = index,
            ["cycle"] = s.ScanNumber,
            ["rt"] = Num.Rt(RawExplorer.RtMinutes(s)),
            ["ms_level"] = Math.Max(1, s.MsLevel),
            ["channel"] = s.ExperimentID,
        };
        var parent = XicSources.ParentMz(s);
        row["parent_mz"] = parent > 0 ? Num.Mz(parent) : null;
        if (parent > 0 && s.Precursor is { } p && (p.IsolationWindowLowerOffset > 0 || p.IsolationWindowUpperOffset > 0))
        {
            row["isolation"] = new JsonObject { ["low"] = Num.Mz(parent - p.IsolationWindowLowerOffset), ["high"] = Num.Mz(parent + p.IsolationWindowUpperOffset) };
        }
        else row["isolation"] = null;
        var ce = CollisionEnergy(s);
        row["ce"] = ce != 0 ? Math.Round(ce, 2) : null;
        row["tic"] = Num.Intensity(s.TotalIonCurrent > 0 ? s.TotalIonCurrent : (s.Spectrum?.Sum(q => q.Intensity) ?? 0));
        row["points"] = s.Spectrum?.Length ?? 0;
        return row;
    }

    public static string F(double v, int decimals) => v.ToString("F" + decimals, CultureInfo.InvariantCulture);

    /// <summary>A folder finding's kind in the contract's spelling: missing-scan, stray-scan, ignored, unsupported, duplicate, unreadable, absent.</summary>
    public static string KindName(MilX.Pipeline.Vendor.FolderFindingKind kind) => kind switch
    {
        MilX.Pipeline.Vendor.FolderFindingKind.MissingScan => "missing-scan",
        MilX.Pipeline.Vendor.FolderFindingKind.StrayScan => "stray-scan",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
