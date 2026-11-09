using System.Globalization;
using CompMs.Common.DataObj;

namespace MilX.Pipeline.Results;

public enum XicSourceKind
{
    /// <summary>The survey (MS1) channel.</summary>
    Ms1,
    /// <summary>One channel by index, as <see cref="RawExplorer.DiscoverChannels"/> numbers them.</summary>
    Channel,
    /// <summary>Every MS2 scan of every channel, merged by retention time.</summary>
    Ms2All,
    /// <summary>Only the MS2 scans whose per-scan precursor is within a tolerance of a mass, from any channel or IDA slot, merged by retention time.</summary>
    Gated,
}

/// <summary>
/// Where a chromatogram or a spectrum is read from. The spelling is the command-line contract's:
/// <c>ms1</c>, <c>channel:N</c>, <c>ms2-all</c>, <c>gated:MZ[:TOL]</c> (TOL in Da, default 0.05).
/// </summary>
public sealed record XicSource(XicSourceKind Kind, int Channel = -1, double GateMz = 0, double GateTolerance = XicSources.DefaultGateTolerance)
{
    public static XicSource Ms1 { get; } = new(XicSourceKind.Ms1);
    public static XicSource Ms2All { get; } = new(XicSourceKind.Ms2All);
    public static XicSource OfChannel(int index) => new(XicSourceKind.Channel, index);
    public static XicSource GatedBy(double mz, double tolerance = XicSources.DefaultGateTolerance) => new(XicSourceKind.Gated, -1, mz, tolerance);

    /// <summary>Parses the contract's spelling; throws <see cref="FormatException"/> on anything else.</summary>
    public static XicSource Parse(string text)
    {
        var t = (text ?? string.Empty).Trim().ToLowerInvariant();
        if (t.Length == 0 || t == "ms1") return Ms1;
        if (t == "ms2-all" || t == "ms2all" || t == "ms2") return Ms2All;
        if (t.StartsWith("channel:", StringComparison.Ordinal))
        {
            if (int.TryParse(t["channel:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0) return OfChannel(n);
            throw new FormatException($"'{text}' is not a source: channel:N needs a channel index");
        }
        if (t.StartsWith("gated:", StringComparison.Ordinal))
        {
            var parts = t["gated:".Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is 1 or 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mz) && mz > 0)
            {
                var tol = XicSources.DefaultGateTolerance;
                if (parts.Length == 2 && !(double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out tol) && tol > 0))
                {
                    throw new FormatException($"'{text}' is not a source: the gate tolerance must be a positive number of daltons");
                }
                return GatedBy(mz, tol);
            }
            throw new FormatException($"'{text}' is not a source: gated:MZ[:TOL] needs a precursor m/z");
        }
        throw new FormatException($"'{text}' is not a source; use ms1, channel:N, ms2-all or gated:MZ[:TOL]");
    }

    /// <summary>The contract's spelling.</summary>
    public override string ToString() => Kind switch
    {
        XicSourceKind.Ms1 => "ms1",
        XicSourceKind.Channel => "channel:" + Channel.ToString(CultureInfo.InvariantCulture),
        XicSourceKind.Ms2All => "ms2-all",
        _ => "gated:" + GateMz.ToString("0.####", CultureInfo.InvariantCulture) + ":" + GateTolerance.ToString("0.####", CultureInfo.InvariantCulture),
    };

    /// <summary>What the interface calls it.</summary>
    public string Label => Kind switch
    {
        XicSourceKind.Ms1 => "MS1",
        XicSourceKind.Channel => $"channel {Channel}",
        XicSourceKind.Ms2All => "all MS2",
        _ => $"MS2 gated {GateMz.ToString("F4", CultureInfo.InvariantCulture)} ± {GateTolerance.ToString("0.###", CultureInfo.InvariantCulture)}",
    };
}

/// <summary>
/// Resolves a <see cref="XicSource"/> to the spectra it names. The gated source is the one that is new
/// in both applications of the suite: a data-dependent file spreads one precursor's product spectra
/// over whichever IDA slot was free that cycle, so the chromatogram of a product ion has to be
/// gathered across the slots by the precursor each scan actually isolated — never by the slot's
/// placeholder mass.
/// </summary>
public static class XicSources
{
    public const double DefaultGateTolerance = 0.05;

    /// <summary>The precursor actually isolated for a scan: the selected ion, or the isolation target when a reader gives only that; 0 for MS1.</summary>
    public static double ParentMz(RawSpectrum s)
    {
        if (s.MsLevel <= 1 || s.Precursor is null) return 0;
        return s.Precursor.SelectedIonMz > 0 ? s.Precursor.SelectedIonMz : s.Precursor.IsolationTargetMz;
    }

    /// <summary>Every MS2 scan (any level above 1), in retention-time order.</summary>
    public static IReadOnlyList<int> Ms2All(RawMeasurement raw)
    {
        var list = new List<int>();
        var spectra = raw.SpectrumList;
        for (var i = 0; i < spectra.Count; i++) if (spectra[i].MsLevel > 1) list.Add(i);
        return InRtOrder(raw, list);
    }

    /// <summary>The MS2 scans whose precursor is within <paramref name="tolerance"/> Da of <paramref name="mz"/>, from any channel, in retention-time order.</summary>
    public static IReadOnlyList<int> Gated(RawMeasurement raw, double mz, double tolerance = DefaultGateTolerance)
    {
        var list = new List<int>();
        var spectra = raw.SpectrumList;
        for (var i = 0; i < spectra.Count; i++)
        {
            var s = spectra[i];
            if (s.MsLevel <= 1) continue;
            var parent = ParentMz(s);
            if (parent > 0 && Math.Abs(parent - mz) <= tolerance) list.Add(i);
        }
        return InRtOrder(raw, list);
    }

    /// <summary>The MS1 indices: the survey channel when the channels are known, otherwise every MS1 scan.</summary>
    public static IReadOnlyList<int> Ms1(RawMeasurement raw, IReadOnlyList<RawChannel>? channels)
    {
        var ms1 = channels?.FirstOrDefault(c => c.Kind == RawChannelKind.Ms1);
        if (ms1 is not null) return ms1.SpectrumIndices;
        var list = new List<int>();
        for (var i = 0; i < raw.SpectrumList.Count; i++) if (raw.SpectrumList[i].MsLevel <= 1) list.Add(i);
        return list;
    }

    /// <summary>The spectra a source names. <paramref name="channels"/> may be null for the sources that do not need them.</summary>
    public static IReadOnlyList<int> Resolve(RawMeasurement raw, IReadOnlyList<RawChannel>? channels, XicSource source)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(source);
        switch (source.Kind)
        {
            case XicSourceKind.Ms1:
                return Ms1(raw, channels);
            case XicSourceKind.Channel:
                channels ??= RawExplorer.DiscoverChannels(raw);
                if (source.Channel < 0 || source.Channel >= channels.Count) throw new ArgumentOutOfRangeException(nameof(source), $"channel {source.Channel} does not exist; the file has {channels.Count}");
                return channels[source.Channel].SpectrumIndices;
            case XicSourceKind.Ms2All:
                return Ms2All(raw);
            default:
                return Gated(raw, source.GateMz, source.GateTolerance);
        }
    }

    /// <summary>Distinct precursors among a set of scans, rounded to 4 decimals, most frequent first.</summary>
    public static IReadOnlyList<(double Mz, int Scans)> Precursors(RawMeasurement raw, IReadOnlyList<int> indices)
    {
        var counts = new Dictionary<long, int>();
        foreach (var i in indices)
        {
            var p = ParentMz(raw.SpectrumList[i]);
            if (p <= 0) continue;
            var key = (long)Math.Round(p * 10000);
            counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
        }
        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => (kv.Key / 10000.0, kv.Value)).ToList();
    }

    /// <summary>
    /// A list of masses as typed or passed on a command line: comma, semicolon, whitespace or any mix of
    /// them separate the values; the decimal mark is a period whatever the locale. Anything that is not a
    /// positive number is skipped and returned in <paramref name="rejected"/>.
    /// </summary>
    public static IReadOnlyList<double> ParseMzList(string? text, out IReadOnlyList<string> rejected)
    {
        var values = new List<double>();
        var bad = new List<string>();
        foreach (var token in (text ?? string.Empty).Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var mz) && mz > 0 && !double.IsInfinity(mz)) values.Add(mz);
            else bad.Add(token);
        }
        rejected = bad;
        return values;
    }

    public static IReadOnlyList<double> ParseMzList(string? text) => ParseMzList(text, out _);

    private static IReadOnlyList<int> InRtOrder(RawMeasurement raw, List<int> indices)
    {
        // the reader sorts spectra by time already; a stable sort by RT keeps that and guards against one that does not
        var spectra = raw.SpectrumList;
        return indices.OrderBy(i => RawExplorer.RtMinutes(spectra[i])).ThenBy(i => i).ToList();
    }
}
