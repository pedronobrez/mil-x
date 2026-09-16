using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;

namespace MilX.Pipeline.Caching;

/// <summary>
/// A raw file reduced to what the interface draws: every spectrum's identity and centroids, without
/// the vendor library. It reconstructs a <see cref="RawMeasurement"/> faithfully enough for the
/// chromatograms, the channel tree and the spectrum views, which is what makes reopening a project
/// immediate.
///
/// It is a display cache, never a processing input: the pipeline always reads the original file, so
/// nothing quantitative depends on what is stored here.
///
/// Masses are tenths of a millidalton in a 32-bit integer — 0.0001 Da, two orders finer than any
/// tolerance a chromatogram is extracted with — which halves both the file and the time to read it.
/// </summary>
public sealed class RawSnapshot
{
    private const uint Magic = 0x32445352;   // "RSD2"
    private const int Version = 2;
    private const double MassScale = 10000.0;

    private readonly ScanHeader[] _headers;
    private readonly int[][] _masses;
    private readonly float[][] _intensities;
    private readonly int[] _ms1Indices;

    private readonly struct ScanHeader
    {
        public ScanHeader(int index, int scanNumber, byte msLevel, short experimentId, byte polarity, double rt,
            float windowLow, float windowHigh, float selectedMz, float isolationMz, float lowerOffset, float upperOffset,
            float collisionEnergy, double totalIonCurrent, double basePeakMz, double basePeakIntensity)
        {
            Index = index; ScanNumber = scanNumber; MsLevel = msLevel; ExperimentId = experimentId; Polarity = polarity;
            Rt = rt; WindowLow = windowLow; WindowHigh = windowHigh; SelectedMz = selectedMz; IsolationMz = isolationMz;
            LowerOffset = lowerOffset; UpperOffset = upperOffset; CollisionEnergy = collisionEnergy;
            TotalIonCurrent = totalIonCurrent; BasePeakMz = basePeakMz; BasePeakIntensity = basePeakIntensity;
        }

        public readonly int Index;
        public readonly int ScanNumber;
        public readonly byte MsLevel;
        public readonly short ExperimentId;
        public readonly byte Polarity;
        public readonly double Rt;
        public readonly float WindowLow;
        public readonly float WindowHigh;
        public readonly float SelectedMz;
        public readonly float IsolationMz;
        public readonly float LowerOffset;
        public readonly float UpperOffset;
        public readonly float CollisionEnergy;
        public readonly double TotalIonCurrent;
        public readonly double BasePeakMz;
        public readonly double BasePeakIntensity;
    }

    private RawSnapshot(ScanHeader[] headers, int[][] masses, float[][] intensities)
    {
        _headers = headers;
        _masses = masses;
        _intensities = intensities;
        _ms1Indices = Enumerable.Range(0, headers.Length).Where(i => headers[i].MsLevel <= 1).ToArray();
    }

    public int ScanCount => _headers.Length;
    public int Ms1Count => _ms1Indices.Length;
    public int MsnCount => _headers.Length - _ms1Indices.Length;
    public long PeakCount => _masses.Sum(m => (long)m.Length);

    public static RawSnapshot FromMeasurement(RawMeasurement raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var spectra = raw.SpectrumList ?? new List<RawSpectrum>();
        var headers = new ScanHeader[spectra.Count];
        var masses = new int[spectra.Count][];
        var intensities = new float[spectra.Count][];
        for (var i = 0; i < spectra.Count; i++)
        {
            var s = spectra[i];
            var precursor = s.Precursor;
            headers[i] = new ScanHeader(
                s.Index, s.ScanNumber, (byte)Math.Clamp(s.MsLevel, 0, 255), (short)Math.Clamp(s.ExperimentID, short.MinValue, short.MaxValue),
                (byte)s.ScanPolarity, RawExplorer.RtMinutes(s),
                (float)s.ScanWindowLowerLimit, (float)s.ScanWindowUpperLimit,
                (float)(precursor?.SelectedIonMz ?? 0), (float)(precursor?.IsolationTargetMz ?? 0),
                (float)(precursor?.IsolationWindowLowerOffset ?? 0), (float)(precursor?.IsolationWindowUpperOffset ?? 0),
                (float)(s.CollisionEnergy > 0 ? s.CollisionEnergy : precursor?.CollisionEnergy ?? 0),
                s.TotalIonCurrent, s.BasePeakMz, s.BasePeakIntensity);
            var peaks = s.Spectrum ?? Array.Empty<RawPeakElement>();
            var m = new int[peaks.Length];
            var v = new float[peaks.Length];
            for (var j = 0; j < peaks.Length; j++)
            {
                m[j] = (int)Math.Round(peaks[j].Mz * MassScale);
                v[j] = (float)peaks[j].Intensity;
            }
            masses[i] = m;
            intensities[i] = v;
        }
        return new RawSnapshot(headers, masses, intensities);
    }

    /// <summary>Rebuilds the measurement the interface reads: same spectra, same order, same indices.</summary>
    public RawMeasurement ToMeasurement()
    {
        var list = new List<RawSpectrum>(_headers.Length);
        for (var i = 0; i < _headers.Length; i++)
        {
            var h = _headers[i];
            var masses = _masses[i];
            var intensities = _intensities[i];
            var peaks = new RawPeakElement[masses.Length];
            for (var j = 0; j < masses.Length; j++)
            {
                peaks[j].Mz = masses[j] / MassScale;
                peaks[j].Intensity = intensities[j];
            }
            var spectrum = new RawSpectrum
            {
                Index = h.Index,
                ScanNumber = h.ScanNumber,
                MsLevel = h.MsLevel,
                ExperimentID = h.ExperimentId,
                ScanPolarity = (ScanPolarity)h.Polarity,
                ScanStartTime = h.Rt,
                ScanStartTimeUnit = Units.Minute,
                ScanWindowLowerLimit = h.WindowLow,
                ScanWindowUpperLimit = h.WindowHigh,
                TotalIonCurrent = h.TotalIonCurrent,
                BasePeakMz = h.BasePeakMz,
                BasePeakIntensity = h.BasePeakIntensity,
                SpectrumRepresentation = SpectrumRepresentation.Centroid,
                Spectrum = peaks,
            };
            if (h.MsLevel > 1 && (h.SelectedMz > 0 || h.IsolationMz > 0))
            {
                spectrum.Precursor = new RawPrecursorIon
                {
                    SelectedIonMz = h.SelectedMz,
                    IsolationTargetMz = h.IsolationMz > 0 ? h.IsolationMz : h.SelectedMz,
                    IsolationWindowLowerOffset = h.LowerOffset,
                    IsolationWindowUpperOffset = h.UpperOffset,
                    CollisionEnergy = h.CollisionEnergy,
                    CollisionEnergyUnit = Units.ElectronVolt,
                    Dissociationmethod = DissociationMethods.CID,
                };
                spectrum.CollisionEnergy = h.CollisionEnergy;
            }
            list.Add(spectrum);
        }
        return new RawMeasurement
        {
            SpectrumList = list,
            AccumulatedSpectrumList = new List<RawSpectrum>(),
            ChromatogramList = new List<RawChromatogram>(),
        };
    }

    /// <summary>
    /// The extracted ion chromatogram over the survey scans, summing every centroid inside the window
    /// of each — the same definition <see cref="RawExplorer.Xic"/> uses on the raw measurement.
    /// </summary>
    public Chromatogram Xic(double mz, double toleranceDa, string? label = null)
    {
        var lo = (int)Math.Round((mz - toleranceDa) * MassScale);
        var hi = (int)Math.Round((mz + toleranceDa) * MassScale);
        var points = new List<ChromatogramPoint>(_ms1Indices.Length);
        foreach (var i in _ms1Indices)
        {
            var masses = _masses[i];
            var intensities = _intensities[i];
            double sum = 0;
            for (var j = LowerBound(masses, lo); j < masses.Length && masses[j] <= hi; j++) sum += intensities[j];
            points.Add(new ChromatogramPoint(_headers[i].Rt, sum));
        }
        return new Chromatogram(label ?? $"XIC {mz:F4} ± {toleranceDa:F4}", mz, toleranceDa, points);
    }

    private static int LowerBound(int[] values, int target)
    {
        int lo = 0, hi = values.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (values[mid] < target) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(_headers.Length);
        foreach (var h in _headers)
        {
            writer.Write(h.Index);
            writer.Write(h.ScanNumber);
            writer.Write(h.MsLevel);
            writer.Write(h.ExperimentId);
            writer.Write(h.Polarity);
            writer.Write(h.Rt);
            writer.Write(h.WindowLow);
            writer.Write(h.WindowHigh);
            writer.Write(h.SelectedMz);
            writer.Write(h.IsolationMz);
            writer.Write(h.LowerOffset);
            writer.Write(h.UpperOffset);
            writer.Write(h.CollisionEnergy);
            writer.Write(h.TotalIonCurrent);
            writer.Write(h.BasePeakMz);
            writer.Write(h.BasePeakIntensity);
        }
        for (var i = 0; i < _headers.Length; i++)
        {
            writer.Write(_masses[i].Length);
            foreach (var m in _masses[i]) writer.Write(m);
            foreach (var v in _intensities[i]) writer.Write(v);
        }
    }

    public static RawSnapshot? Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic) return null;
        if (reader.ReadInt32() != Version) return null;
        var count = reader.ReadInt32();
        if (count < 0 || count > 20_000_000) return null;
        var headers = new ScanHeader[count];
        for (var i = 0; i < count; i++)
        {
            headers[i] = new ScanHeader(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte(), reader.ReadInt16(), reader.ReadByte(),
                reader.ReadDouble(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        }
        var masses = new int[count][];
        var intensities = new float[count][];
        for (var i = 0; i < count; i++)
        {
            var peaks = reader.ReadInt32();
            if (peaks < 0 || peaks > 50_000_000) return null;
            var m = new int[peaks];
            for (var j = 0; j < peaks; j++) m[j] = reader.ReadInt32();
            var v = new float[peaks];
            for (var j = 0; j < peaks; j++) v[j] = reader.ReadSingle();
            masses[i] = m;
            intensities[i] = v;
        }
        return new RawSnapshot(headers, masses, intensities);
    }
}
