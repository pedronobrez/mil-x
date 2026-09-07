using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Xml;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;

namespace OpenDIAL.RawData.Mzml;

/// <summary>Options that deliberately deviate from (improve on) the closed reader's behaviour.</summary>
public sealed class MzmlReaderOptions
{
    /// <summary>
    /// When a spectrum carries no spectrum-level collision energy but its precursor
    /// activation does, copy the activation energy into RawSpectrum.CollisionEnergy so
    /// that CE-aware code paths (AIF/MSE grouping, MSP export) see the real value.
    /// </summary>
    public bool CopyPrecursorCollisionEnergy { get; set; } = true;

    /// <summary>Use the isolation window target m/z when a precursor has no selected ion m/z.</summary>
    public bool FallbackSelectedIonToIsolationTarget { get; set; } = true;

    /// <summary>Compute TIC from the peak list when the file does not provide MS:1000285.</summary>
    public bool ComputeMissingTotalIonCurrent { get; set; } = true;

    /// <summary>Optional progress sink (0..100).</summary>
    public IProgress<double>? Progress { get; set; }

    /// <summary>Optional log sink.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Echo "i / N" progress on the console like the closed reader (only when not a GUI process).</summary>
    public bool ConsoleProgress { get; set; } = true;
}

/// <summary>
/// Streaming mzML 1.1 / indexedmzML reader producing MS-DIAL's <see cref="RawMeasurement"/>.
/// It reads the file once with an <see cref="XmlReader"/>, never loads the document in memory,
/// and supports zlib and MS-Numpress compressed arrays, 32/64-bit floats and integers,
/// referenceable parameter groups, chromatograms, and ion mobility drift times.
/// </summary>
public sealed class MzmlReader
{
    private readonly MzmlReaderOptions _options;
    private readonly Dictionary<string, List<CvParam>> _paramGroups = new(StringComparer.Ordinal);

    public MzmlReader() : this(new MzmlReaderOptions()) { }

    public MzmlReader(MzmlReaderOptions options) {
        _options = options ?? new MzmlReaderOptions();
    }

    public bool IsIonMobilityData { get; private set; }
    public int SpectraCount { get; private set; }
    public List<RawSpectrum> SpectraList { get; private set; } = new();
    public List<RawChromatogram> ChromatogramsList { get; private set; } = new();
    public List<RawSourceFileInfo> SourceFiles { get; } = new();
    public List<RawSample> Samples { get; } = new();
    public DateTime StartTimeStamp { get; private set; }

    public readonly struct CvParam
    {
        public CvParam(string accession, string? value, string? unitAccession, string? name) {
            Accession = accession;
            Value = value;
            UnitAccession = unitAccession;
            Name = name;
        }
        public string Accession { get; }
        public string? Value { get; }
        public string? UnitAccession { get; }
        public string? Name { get; }

        public double DoubleValue => double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
        public int IntValue => int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int)DoubleValue;
    }

    /// <summary>Reads a whole mzML file. Signature compatible with the closed reader.</summary>
    public RawMeasurement ReadMzml(string inputMzMLfilePath, int fileID, bool isGuiProcess, BackgroundWorker? bgWorker = null) {
        SpectraList = new List<RawSpectrum>();
        ChromatogramsList = new List<RawChromatogram>();
        SourceFiles.Clear();
        Samples.Clear();
        _paramGroups.Clear();
        IsIonMobilityData = false;
        SpectraCount = 0;

        var settings = new XmlReaderSettings {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false,
        };
        using (var stream = new FileStream(inputMzMLfilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
        using (var reader = XmlReader.Create(stream, settings)) {
            var lastReported = -1;
            while (reader.Read()) {
                if (reader.NodeType != XmlNodeType.Element) {
                    continue;
                }
                switch (reader.LocalName) {
                    case "referenceableParamGroup":
                        ParseReferenceableParamGroup(reader);
                        break;
                    case "sourceFile": {
                        SourceFiles.Add(new RawSourceFileInfo {
                            Id = reader.GetAttribute("id")!,
                            Name = reader.GetAttribute("name")!,
                            Location = reader.GetAttribute("location")!,
                        });
                        break;
                    }
                    case "sample":
                        Samples.Add(new RawSample { Id = reader.GetAttribute("id")!, Name = reader.GetAttribute("name")! });
                        break;
                    case "run": {
                        var ts = reader.GetAttribute("startTimeStamp");
                        if (ts != null && DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) {
                            StartTimeStamp = dt;
                        }
                        break;
                    }
                    case "spectrumList": {
                        var count = reader.GetAttribute("count");
                        if (int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) {
                            SpectraCount = n;
                            if (SpectraList.Capacity < n) SpectraList.Capacity = n;
                        }
                        break;
                    }
                    case "spectrum": {
                        using (var sub = reader.ReadSubtree()) {
                            sub.Read();
                            var spectrum = ParseSpectrum(sub);
                            SpectraList.Add(spectrum);
                        }
                        ReportProgress(SpectraList.Count, SpectraCount, isGuiProcess, bgWorker, ref lastReported);
                        break;
                    }
                    case "chromatogram": {
                        using (var sub = reader.ReadSubtree()) {
                            sub.Read();
                            var chromatogram = ParseChromatogram(sub);
                            if (chromatogram != null) ChromatogramsList.Add(chromatogram);
                        }
                        break;
                    }
                    case "indexList":
                    case "indexListOffset":
                    case "fileChecksum":
                        // trailing index of indexedmzML: nothing more to read
                        goto done;
                }
            }
        }
        done:
        if (!isGuiProcess && bgWorker == null && _options.ConsoleProgress && !Console.IsOutputRedirected) {
            Console.WriteLine();
        }
        var measurement = SpectrumParser.GetRawMeasurementObj(inputMzMLfilePath, fileID, SpectraList);
        measurement.ChromatogramList = ChromatogramsList;
        measurement.AccumulatedSpectrumList ??= new List<RawSpectrum>();
        return measurement;
    }

    private void ReportProgress(int current, int total, bool isGuiProcess, BackgroundWorker? bgWorker, ref int lastReported) {
        if (total <= 0) total = Math.Max(total, current);
        var percent = total > 0 ? (int)(100.0 * current / total) : 0;
        if (bgWorker != null) {
            if (percent != lastReported) {
                bgWorker.ReportProgress(percent);
                lastReported = percent;
            }
            return;
        }
        _options.Progress?.Report(percent);
        if (isGuiProcess || !_options.ConsoleProgress) {
            return;
        }
        if (Console.IsOutputRedirected) {
            // one line per 5% keeps logs readable
            if (percent / 5 != lastReported / 5 || current == total) {
                Console.WriteLine("{0} / {1}", current, total);
                lastReported = percent;
            }
        }
        else if (percent != lastReported || current == total) {
            Console.Write("\r{0} / {1}", current, total);
            lastReported = percent;
        }
    }

    // ------------------------------------------------------------------
    // cvParam helpers
    // ------------------------------------------------------------------

    private static CvParam ReadCvParam(XmlReader r) {
        return new CvParam(r.GetAttribute("accession") ?? string.Empty, r.GetAttribute("value"), r.GetAttribute("unitAccession"), r.GetAttribute("name"));
    }

    private void ParseReferenceableParamGroup(XmlReader r) {
        var id = r.GetAttribute("id") ?? string.Empty;
        var list = new List<CvParam>();
        if (!r.IsEmptyElement) {
            using var sub = r.ReadSubtree();
            sub.Read();
            while (sub.Read()) {
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "cvParam") {
                    list.Add(ReadCvParam(sub));
                }
            }
        }
        _paramGroups[id] = list;
    }

    private IEnumerable<CvParam> ResolveGroup(XmlReader r) {
        var reference = r.GetAttribute("ref") ?? string.Empty;
        if (_paramGroups.TryGetValue(reference, out var list)) {
            return list;
        }
        // MS-DIAL's own writers use these two names without defining the group.
        if (reference == "SpectrumParamsPositive") return new[] { new CvParam("MS:1000130", null, null, "positive scan") };
        if (reference == "SpectrumParamsNegative") return new[] { new CvParam("MS:1000129", null, null, "negative scan") };
        return Array.Empty<CvParam>();
    }

    private static Units UnitFromAccession(string? unitAccession) {
        switch (unitAccession) {
            case "UO:0000010": return Units.Second;
            case "UO:0000031": return Units.Minute;
            case "UO:0000028": return Units.Milliseconds;
            case "UO:0000266": return Units.ElectronVolt;
            case "MS:1000040": return Units.Mz;
            case "MS:1000131": return Units.NumberOfCounts;
            case "MS:1002814": return Units.Oneoverk0;
            default: return Units.Undefined;
        }
    }

    private static DissociationMethods DissociationFromAccession(string accession) {
        switch (accession) {
            case "MS:1000133": return DissociationMethods.CID;
            case "MS:1000134": return DissociationMethods.BIRD;
            case "MS:1000135": return DissociationMethods.PD;
            case "MS:1000136": return DissociationMethods.PSD;
            case "MS:1000137": return DissociationMethods.SID;
            case "MS:1000250": return DissociationMethods.ECD;
            case "MS:1000262": return DissociationMethods.IRMPD;
            case "MS:1000282": return DissociationMethods.SORI;
            case "MS:1000422": return DissociationMethods.HCD;
            case "MS:1000433": return DissociationMethods.LowEnergyCID;
            case "MS:1000435": return DissociationMethods.MPD;
            case "MS:1000598": return DissociationMethods.ETD;
            case "MS:1000599": return DissociationMethods.PQD;
            case "MS:1001880": return DissociationMethods.InSourceCID;
            case "MS:1002000": return DissociationMethods.LIFT;
            default: return DissociationMethods.Undefined;
        }
    }

    private static bool IsDissociationAccession(string accession) {
        return DissociationFromAccession(accession) != DissociationMethods.Undefined;
    }

    // ------------------------------------------------------------------
    // <spectrum>
    // ------------------------------------------------------------------

    private RawSpectrum ParseSpectrum(XmlReader r) {
        var spectrum = new RawSpectrum {
            Id = r.GetAttribute("id")!,
        };
        if (int.TryParse(r.GetAttribute("defaultArrayLength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var len)) {
            spectrum.DefaultArrayLength = len;
        }
        if (int.TryParse(r.GetAttribute("index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) {
            spectrum.ScanNumber = index;
        }
        var hasTic = false;
        var hasSpectrumLevelCe = false;
        var hasScanStartTime = false;
        var arrays = new List<(BinaryArrayDescriptor desc, double[] values)>(2);

        if (!r.IsEmptyElement) {
            while (r.Read()) {
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName) {
                    case "cvParam":
                        if (r.Depth == 1) {
                            var cv = ReadCvParam(r);
                            ApplySpectrumCv(spectrum, cv, ref hasTic, ref hasSpectrumLevelCe, ref hasScanStartTime);
                        }
                        break;
                    case "referenceableParamGroupRef":
                        if (r.Depth == 1) {
                            foreach (var cv in ResolveGroup(r)) {
                                ApplySpectrumCv(spectrum, cv, ref hasTic, ref hasSpectrumLevelCe, ref hasScanStartTime);
                            }
                        }
                        break;
                    case "scanList": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        ParseScanList(sub, spectrum, ref hasScanStartTime);
                        break;
                    }
                    case "precursorList": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        ParsePrecursorList(sub, spectrum);
                        break;
                    }
                    case "productList": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        ParseProductList(sub, spectrum);
                        break;
                    }
                    case "binaryDataArrayList": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        ParseBinaryDataArrayList(sub, arrays, spectrum.DefaultArrayLength);
                        break;
                    }
                }
            }
        }

        FinalizeSpectrum(spectrum, arrays, hasTic);

        if (_options.CopyPrecursorCollisionEnergy && !hasSpectrumLevelCe && spectrum.Precursor != null && spectrum.Precursor.CollisionEnergy > 0) {
            spectrum.CollisionEnergy = spectrum.Precursor.CollisionEnergy;
        }
        if (_options.FallbackSelectedIonToIsolationTarget && spectrum.Precursor != null && spectrum.Precursor.SelectedIonMz <= 0 && spectrum.Precursor.IsolationTargetMz > 0) {
            spectrum.Precursor.SelectedIonMz = spectrum.Precursor.IsolationTargetMz;
        }
        return spectrum;
    }

    private void ApplySpectrumCv(RawSpectrum spectrum, CvParam cv, ref bool hasTic, ref bool hasSpectrumLevelCe, ref bool hasScanStartTime) {
        switch (cv.Accession) {
            case "MS:1000511": spectrum.MsLevel = cv.IntValue; break;
            case "MS:1000129": spectrum.ScanPolarity = ScanPolarity.Negative; break;
            case "MS:1000130": spectrum.ScanPolarity = ScanPolarity.Positive; break;
            case "MS:1000127": spectrum.SpectrumRepresentation = SpectrumRepresentation.Centroid; break;
            case "MS:1000128": spectrum.SpectrumRepresentation = SpectrumRepresentation.Profile; break;
            case "MS:1000504": spectrum.BasePeakMz = cv.DoubleValue; break;
            case "MS:1000505": spectrum.BasePeakIntensity = cv.DoubleValue; break;
            case "MS:1000285": spectrum.TotalIonCurrent = cv.DoubleValue; hasTic = true; break;
            case "MS:1000527": spectrum.HighestObservedMz = cv.DoubleValue; break;
            case "MS:1000528": spectrum.LowestObservedMz = cv.DoubleValue; break;
            case "MS:1000045": spectrum.CollisionEnergy = cv.DoubleValue; hasSpectrumLevelCe = true; break;
            case "MS:1000016": ApplyScanStartTime(spectrum, cv, ref hasScanStartTime); break;
            case "MS:1002476": ApplyDriftTime(spectrum, cv, Units.Milliseconds); break;
            case "MS:1002815": ApplyDriftTime(spectrum, cv, Units.Oneoverk0); break;
        }
    }

    private void ApplyScanStartTime(RawSpectrum spectrum, CvParam cv, ref bool hasScanStartTime) {
        if (hasScanStartTime) {
            return; // the first scan of a combined spectrum defines its time
        }
        hasScanStartTime = true;
        spectrum.ScanStartTime = cv.DoubleValue;
        spectrum.ScanStartTimeUnit = UnitFromAccession(cv.UnitAccession);
        if (spectrum.ScanStartTimeUnit == Units.Second) {
            spectrum.ScanStartTime /= 60.0;
            spectrum.ScanStartTimeUnit = Units.Minute;
        }
    }

    private void ApplyDriftTime(RawSpectrum spectrum, CvParam cv, Units defaultUnit) {
        spectrum.DriftTime = cv.DoubleValue;
        var unit = UnitFromAccession(cv.UnitAccession);
        spectrum.DriftTimeUnit = unit == Units.Undefined ? defaultUnit : unit;
        IsIonMobilityData = true;
    }

    private void ParseScanList(XmlReader r, RawSpectrum spectrum, ref bool hasScanStartTime) {
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "scan") continue;
            using var sub = r.ReadSubtree();
            sub.Read();
            ParseScan(sub, spectrum, ref hasScanStartTime);
        }
    }

    private void ParseScan(XmlReader r, RawSpectrum spectrum, ref bool hasScanStartTime) {
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName) {
                case "cvParam":
                    if (r.Depth == 1) ApplyScanCv(spectrum, ReadCvParam(r), ref hasScanStartTime);
                    break;
                case "referenceableParamGroupRef":
                    if (r.Depth == 1) {
                        foreach (var cv in ResolveGroup(r)) ApplyScanCv(spectrum, cv, ref hasScanStartTime);
                    }
                    break;
                case "scanWindowList": {
                    using var sub = r.ReadSubtree();
                    sub.Read();
                    while (sub.Read()) {
                        if (sub.NodeType != XmlNodeType.Element || sub.LocalName != "cvParam") continue;
                        var cv = ReadCvParam(sub);
                        switch (cv.Accession) {
                            case "MS:1000501": spectrum.ScanWindowLowerLimit = cv.DoubleValue; break;
                            case "MS:1000500": spectrum.ScanWindowUpperLimit = cv.DoubleValue; break;
                        }
                    }
                    break;
                }
            }
        }
    }

    private void ApplyScanCv(RawSpectrum spectrum, CvParam cv, ref bool hasScanStartTime) {
        switch (cv.Accession) {
            case "MS:1000016": ApplyScanStartTime(spectrum, cv, ref hasScanStartTime); break;
            case "MS:1002476": ApplyDriftTime(spectrum, cv, Units.Milliseconds); break;
            case "MS:1002815": ApplyDriftTime(spectrum, cv, Units.Oneoverk0); break;
            case "MS:1000129": spectrum.ScanPolarity = ScanPolarity.Negative; break;
            case "MS:1000130": spectrum.ScanPolarity = ScanPolarity.Positive; break;
        }
    }

    private void ParsePrecursorList(XmlReader r, RawSpectrum spectrum) {
        RawPrecursorIon? last = null;
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "precursor") continue;
            using var sub = r.ReadSubtree();
            sub.Read();
            last = ParsePrecursor(sub);
        }
        spectrum.Precursor = last;
    }

    private RawPrecursorIon ParsePrecursor(XmlReader r) {
        var precursor = new RawPrecursorIon();
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName) {
                case "isolationWindow": {
                    using var sub = r.ReadSubtree();
                    sub.Read();
                    foreach (var cv in ReadCvParams(sub)) {
                        switch (cv.Accession) {
                            case "MS:1000827": precursor.IsolationTargetMz = cv.DoubleValue; break;
                            case "MS:1000828": precursor.IsolationWindowLowerOffset = cv.DoubleValue; break;
                            case "MS:1000829": precursor.IsolationWindowUpperOffset = cv.DoubleValue; break;
                        }
                    }
                    break;
                }
                case "selectedIonList": {
                    using var sub = r.ReadSubtree();
                    sub.Read();
                    foreach (var cv in ReadCvParams(sub)) {
                        if (cv.Accession == "MS:1000744") precursor.SelectedIonMz = cv.DoubleValue;
                    }
                    break;
                }
                case "activation": {
                    using var sub = r.ReadSubtree();
                    sub.Read();
                    foreach (var cv in ReadCvParams(sub)) {
                        if (cv.Accession == "MS:1000045") {
                            precursor.CollisionEnergy = cv.DoubleValue;
                            precursor.CollisionEnergyUnit = UnitFromAccession(cv.UnitAccession);
                        }
                        else if (IsDissociationAccession(cv.Accession)) {
                            precursor.Dissociationmethod = DissociationFromAccession(cv.Accession);
                        }
                    }
                    break;
                }
            }
        }
        return precursor;
    }

    private void ParseProductList(XmlReader r, RawSpectrum spectrum) {
        RawProductIon? last = null;
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "product") continue;
            using var sub = r.ReadSubtree();
            sub.Read();
            var product = new RawProductIon();
            foreach (var cv in ReadCvParams(sub)) {
                switch (cv.Accession) {
                    case "MS:1000827": product.IsolationTargetMz = cv.DoubleValue; break;
                    case "MS:1000828": product.IsolationWindowLowerOffset = cv.DoubleValue; break;
                    case "MS:1000829": product.IsolationWindowUpperOffset = cv.DoubleValue; break;
                }
            }
            last = product;
        }
        spectrum.Product = last;
    }

    /// <summary>Enumerates every cvParam (and referenced group params) under the current subtree.</summary>
    private IEnumerable<CvParam> ReadCvParams(XmlReader sub) {
        while (sub.Read()) {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "cvParam") {
                yield return ReadCvParam(sub);
            }
            else if (sub.LocalName == "referenceableParamGroupRef") {
                foreach (var cv in ResolveGroup(sub)) yield return cv;
            }
        }
    }

    // ------------------------------------------------------------------
    // binary arrays
    // ------------------------------------------------------------------

    private void ParseBinaryDataArrayList(XmlReader r, List<(BinaryArrayDescriptor, double[])> arrays, int defaultArrayLength) {
        while (r.Read()) {
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "binaryDataArray") continue;
            using var sub = r.ReadSubtree();
            sub.Read();
            var (desc, values) = ParseBinaryDataArray(sub);
            arrays.Add((desc, values));
        }
    }

    private (BinaryArrayDescriptor, double[]) ParseBinaryDataArray(XmlReader r) {
        var desc = new BinaryArrayDescriptor();
        if (int.TryParse(r.GetAttribute("encodedLength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var enc)) desc.EncodedLength = enc;
        if (int.TryParse(r.GetAttribute("arrayLength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var al)) desc.ArrayLength = al;
        double[] values = Array.Empty<double>();
        var pendingBinary = false;
        while (pendingBinary || r.Read()) {
            pendingBinary = false;
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName) {
                case "cvParam": {
                    var cv = ReadCvParam(r);
                    desc.ApplyAccession(cv.Accession);
                    if (cv.UnitAccession != null) desc.ApplyUnitAccession(cv.UnitAccession);
                    break;
                }
                case "referenceableParamGroupRef":
                    foreach (var cv in ResolveGroup(r)) {
                        desc.ApplyAccession(cv.Accession);
                        if (cv.UnitAccession != null) desc.ApplyUnitAccession(cv.UnitAccession);
                    }
                    break;
                case "binary": {
                    var text = r.IsEmptyElement ? string.Empty : r.ReadElementContentAsString();
                    // ReadElementContentAsString leaves the reader on the node after </binary>;
                    // do not advance again before inspecting it.
                    pendingBinary = true;
                    values = desc.Decode(text);
                    break;
                }
            }
            if (pendingBinary && r.EOF) break;
        }
        return (desc, values);
    }

    private void FinalizeSpectrum(RawSpectrum spectrum, List<(BinaryArrayDescriptor desc, double[] values)> arrays, bool hasTic) {
        double[]? mz = null;
        double[]? intensity = null;
        foreach (var (desc, values) in arrays) {
            if (desc.Kind == BinaryArrayKind.Mz) mz = values;
            else if (desc.Kind == BinaryArrayKind.Intensity) intensity = values;
        }
        if (mz == null || intensity == null || mz.Length == 0 || intensity.Length == 0) {
            SetEmpty(spectrum);
            return;
        }
        var n = Math.Min(mz.Length, intensity.Length);
        if (mz.Length != intensity.Length) {
            _options.Log?.Invoke($"warning: spectrum {spectrum.Id}: m/z ({mz.Length}) and intensity ({intensity.Length}) array lengths differ; truncated to {n}");
        }
        // The closed reader drops a trailing zero m/z value produced by some converters.
        if (n > 0 && (int)mz[n - 1] == 0) n--;
        if (n == 0) {
            SetEmpty(spectrum);
            return;
        }
        var peaks = new RawPeakElement[n];
        var minIntensity = double.MaxValue;
        var maxIntensity = double.MinValue;
        var basePeakMz = 0.0;
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        var tic = 0.0;
        for (var i = 0; i < n; i++) {
            var m = mz[i];
            var it = intensity[i];
            peaks[i].Mz = m;
            peaks[i].Intensity = it;
            tic += it;
            if (it < minIntensity) minIntensity = it;
            if (it > maxIntensity) { maxIntensity = it; basePeakMz = m; }
            if (m < lowest) lowest = m;
            if (m > highest) highest = m;
        }
        spectrum.Spectrum = peaks;
        spectrum.DefaultArrayLength = n;
        spectrum.MinIntensity = minIntensity;
        spectrum.LowestObservedMz = lowest;
        spectrum.HighestObservedMz = highest;
        spectrum.BasePeakMz = basePeakMz;
        spectrum.BasePeakIntensity = maxIntensity;
        if (!hasTic && _options.ComputeMissingTotalIonCurrent) {
            spectrum.TotalIonCurrent = tic;
        }
    }

    private static void SetEmpty(RawSpectrum spectrum) {
        spectrum.Spectrum = Array.Empty<RawPeakElement>();
        spectrum.DefaultArrayLength = 0;
        spectrum.MinIntensity = 0.0;
        spectrum.LowestObservedMz = spectrum.ScanWindowLowerLimit;
        spectrum.HighestObservedMz = spectrum.ScanWindowUpperLimit;
    }

    // ------------------------------------------------------------------
    // <chromatogram>
    // ------------------------------------------------------------------

    private RawChromatogram? ParseChromatogram(XmlReader r) {
        var chromatogram = new RawChromatogram { Id = r.GetAttribute("id")! };
        if (int.TryParse(r.GetAttribute("index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) chromatogram.Index = index;
        if (int.TryParse(r.GetAttribute("defaultArrayLength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var len)) chromatogram.DefaultArrayLength = len;
        var arrays = new List<(BinaryArrayDescriptor desc, double[] values)>(2);
        if (!r.IsEmptyElement) {
            while (r.Read()) {
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName) {
                    case "cvParam":
                        if (r.Depth == 1 && ReadCvParam(r).Accession == "MS:1001473") chromatogram.IsSRM = true;
                        break;
                    case "precursor": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        var p = new RawPrecursorIon();
                        foreach (var cv in ReadCvParams(sub)) {
                            switch (cv.Accession) {
                                case "MS:1000827": p.IsolationTargetMz = cv.DoubleValue; break;
                                case "MS:1000828": p.IsolationWindowLowerOffset = cv.DoubleValue; break;
                                case "MS:1000829": p.IsolationWindowUpperOffset = cv.DoubleValue; break;
                                case "MS:1000744": p.SelectedIonMz = cv.DoubleValue; break;
                                case "MS:1000045": p.CollisionEnergy = cv.DoubleValue; break;
                            }
                        }
                        chromatogram.Precursor = p;
                        break;
                    }
                    case "product": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        var p = new RawProductIon();
                        foreach (var cv in ReadCvParams(sub)) {
                            switch (cv.Accession) {
                                case "MS:1000827": p.IsolationTargetMz = cv.DoubleValue; break;
                                case "MS:1000828": p.IsolationWindowLowerOffset = cv.DoubleValue; break;
                                case "MS:1000829": p.IsolationWindowUpperOffset = cv.DoubleValue; break;
                            }
                        }
                        chromatogram.Product = p;
                        break;
                    }
                    case "binaryDataArrayList": {
                        using var sub = r.ReadSubtree();
                        sub.Read();
                        ParseBinaryDataArrayList(sub, arrays, chromatogram.DefaultArrayLength);
                        break;
                    }
                }
            }
        }
        double[]? time = null;
        double[]? intensity = null;
        var timeUnit = BinaryUnit.Unknown;
        foreach (var (desc, values) in arrays) {
            if (desc.Kind == BinaryArrayKind.Time) { time = values; timeUnit = desc.Unit; }
            else if (desc.Kind == BinaryArrayKind.Intensity) intensity = values;
        }
        if (time == null || intensity == null) {
            chromatogram.Chromatogram = Array.Empty<RawChromatogramElement>();
            chromatogram.DefaultArrayLength = 0;
            return chromatogram;
        }
        var n = Math.Min(time.Length, intensity.Length);
        var points = new RawChromatogramElement[n];
        for (var i = 0; i < n; i++) {
            var t = time[i];
            if (timeUnit == BinaryUnit.Second) t /= 60.0;
            else if (timeUnit == BinaryUnit.Millisecond) t /= 60000.0;
            points[i].RtInMin = t;
            points[i].Intensity = intensity[i];
        }
        chromatogram.Chromatogram = points;
        chromatogram.DefaultArrayLength = n;
        return chromatogram;
    }
}
