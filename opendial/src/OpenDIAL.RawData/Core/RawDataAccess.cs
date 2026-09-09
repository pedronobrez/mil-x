using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CompMs.Common.DataObj;
using OpenDIAL.RawData.Legacy;
using OpenDIAL.RawData.Mzml;
using OpenDIAL.RawData.Plugins;
using OpenDIAL.RawData.Vendor;

namespace CompMs.RawDataHandler.Core;

/// <summary>Process-wide settings for <see cref="RawDataAccess"/>.</summary>
public static class RawDataAccessOptions
{
    /// <summary>Log sink for reader and converter messages (default: Console.WriteLine).</summary>
    public static Action<string> Log { get; set; } = Console.WriteLine;

    /// <summary>Options applied to every mzML read.</summary>
    public static MzmlReaderOptions Mzml { get; set; } = new MzmlReaderOptions();

    /// <summary>The vendor-format bridge in use (msconvert native or Docker).</summary>
    public static VendorConverter Vendor {
        get => VendorConverter.Default;
        set => VendorConverter.Default = value;
    }

    /// <summary>When false, vendor files are never converted implicitly and GetMeasurement returns null for them.</summary>
    public static bool AllowImplicitVendorConversion { get; set; } = true;
}

/// <summary>
/// Entry point used by MS-DIAL to read raw data. The public surface mirrors the closed
/// RawDataHandler package so that the upstream code base compiles against either.
/// </summary>
public class RawDataAccess : IDisposable
{
    private bool _disposed;
    private bool _isDataSupported = true;
    private readonly bool _getProfileData;
    private readonly bool _isImagingMsData;
    private readonly bool _isGuiProcess;
    private readonly List<double>? _correctedRtList;
    private readonly BackgroundWorker? _bgWorker;
    private readonly string _extensionString = string.Empty;

    private static bool IsVendorExtension(RawDataExtension ext) =>
        ext is RawDataExtension.raw or RawDataExtension.d or RawDataExtension.wiff or RawDataExtension.wiff2 or RawDataExtension.lcd or RawDataExtension.qgd or RawDataExtension.lrp;
    private LegacyReaderPlugin.LegacyAccessHandle? _legacyHandle;

    public double PeakCutOff { get; set; }
    public double MzToleranceForPixelData { get; set; } = 0.02;
    public double DriftToleranceForPixelData { get; set; } = 0.015;
    public RawMeasurement? Measurement { get; set; }
    public RawDataExtension Extension { get; set; }
    public string Filepath { get; set; } = string.Empty;
    public bool IsDataSupported { get => _isDataSupported; set => _isDataSupported = value; }
    public int FileID { get; set; }

    public RawDataAccess(string filepath, int fileID, bool getProfileData, bool isImagingMsData, bool isGuiProcess,
        double peakCutoff, double mzToleranceForPixelData, double driftToleranceForPixelData,
        List<double>? correctedRts = null, BackgroundWorker? bgWorker = null)
        : this(filepath, fileID, getProfileData, isImagingMsData, isGuiProcess, correctedRts, bgWorker) {
        PeakCutOff = peakCutoff;
        MzToleranceForPixelData = mzToleranceForPixelData;
        DriftToleranceForPixelData = driftToleranceForPixelData;
    }

    public RawDataAccess(string filepath, int fileID, bool getProfileData, bool isImagingMsData, bool isGuiProcess,
        List<double>? correctedRts = null, BackgroundWorker? bgWorker = null) {
        Filepath = filepath;
        FileID = fileID;
        _isGuiProcess = isGuiProcess;
        _isImagingMsData = isImagingMsData;
        _correctedRtList = correctedRts;
        _bgWorker = bgWorker;
        _getProfileData = getProfileData;
        _extensionString = Path.GetExtension(filepath ?? string.Empty).ToLowerInvariant();
        switch (_extensionString) {
            case ".abf": Extension = RawDataExtension.abf; break;
            case ".mzml": Extension = RawDataExtension.mzml; break;
            case ".imzml": Extension = RawDataExtension.imzml; break;
            case ".cdf": Extension = RawDataExtension.cdf; break;
            case ".raw": Extension = RawDataExtension.raw; break;
            case ".d": Extension = RawDataExtension.d; break;
            case ".iabf":
            case ".ibf": Extension = RawDataExtension.ibf; break;
            case ".wiff2": Extension = RawDataExtension.wiff2; break;
            case ".wiff": Extension = RawDataExtension.wiff; break;
            case ".lcd": Extension = RawDataExtension.lcd; break;
            case ".qgd": Extension = RawDataExtension.qgd; break;
            case ".lrp": Extension = RawDataExtension.lrp; break;
            default: _isDataSupported = false; break;
        }
    }

    public RawDataAccess() { }

    /// <summary>
    /// Reads the whole measurement. Returns null (after logging) when the format cannot be
    /// read on this machine; MS-DIAL's data providers retry and then throw FileLoadException.
    /// </summary>
    public RawMeasurement? GetMeasurement() {
        if (!_isDataSupported) {
            RawDataAccessOptions.Log("Raw data format is not supported: " + _extensionString);
            return null;
        }
        try {
            RawDataAccessOptions.Log("Reading data...");
            // 1. explicit plugins (e.g. a Thermo RawFileReader plugin)
            var plugin = RawReaderPlugins.Find(Filepath);
            if (plugin != null) {
                RawDataAccessOptions.Log($"[reader] {plugin.Name}: {Filepath}");
                try {
                    var measurement = plugin.Read(Filepath, FileID, new RawReadOptions {
                        GetProfileData = _getProfileData,
                        IsImagingMsData = _isImagingMsData,
                        IsGuiProcess = _isGuiProcess,
                        PeakCutOff = PeakCutOff,
                        Log = RawDataAccessOptions.Log,
                    });
                    return Measurement = ApplyRtCorrection(measurement);
                }
                catch (Exception ex) when (IsVendorExtension(Extension)) {
                    // the plugin claimed the file and could not read it: say so and let the vendor
                    // bridge have it, rather than failing the whole run on one reader's limits
                    RawDataAccessOptions.Log($"[reader] {plugin.Name} could not read {Path.GetFileName(Filepath)} ({ex.GetType().Name}: {ex.Message}); trying the vendor bridge");
                }
            }
            switch (Extension) {
                case RawDataExtension.mzml:
                    return Measurement = ApplyRtCorrection(ReadMzml(Filepath));
                case RawDataExtension.raw:
                case RawDataExtension.d:
                case RawDataExtension.wiff:
                case RawDataExtension.wiff2:
                case RawDataExtension.lcd:
                case RawDataExtension.qgd:
                case RawDataExtension.lrp: {
                    // On Windows a vendor-enabled legacy dll can read these directly.
                    var legacy = LegacyReaderPlugin.Default;
                    if (legacy != null && legacy.AssemblyName.Equals("RawDataHandler", StringComparison.OrdinalIgnoreCase) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        return Measurement = ApplyRtCorrection(legacy.GetMeasurement(Filepath, FileID, _getProfileData, _isImagingMsData, _isGuiProcess, null, _bgWorker));
                    }
                    if (!RawDataAccessOptions.AllowImplicitVendorConversion) {
                        RawDataAccessOptions.Log($"Vendor format {_extensionString} requires conversion to mzML (implicit conversion disabled).");
                        return null;
                    }
                    var mzml = RawDataAccessOptions.Vendor.EnsureMzml(Filepath);
                    return Measurement = ApplyRtCorrection(ReadMzml(mzml));
                }
                case RawDataExtension.abf:
                case RawDataExtension.ibf:
                case RawDataExtension.cdf:
                case RawDataExtension.imzml: {
                    var legacy = LegacyReaderPlugin.Default;
                    if (legacy == null) {
                        RawDataAccessOptions.Log(
                            $"Format {_extensionString} is not implemented by OpenDIAL.RawData. Place the original RawDataHandler dll in <app>/plugins/legacy (or set OPENDIAL_LEGACY_RAWDATA_DLL) to read it, or convert the file to mzML.");
                        return null;
                    }
                    RawDataAccessOptions.Log($"[reader] legacy {legacy.AssemblyName}: {Filepath}");
                    return Measurement = ApplyRtCorrection(legacy.GetMeasurement(Filepath, FileID, _getProfileData, _isImagingMsData, _isGuiProcess, null, _bgWorker));
                }
            }
        }
        catch (Exception ex) {
            RawDataAccessOptions.Log("Error: " + ex.Message);
            RawDataAccessOptions.Log(ex.ToString());
            return null;
        }
        return null;
    }

    private RawMeasurement ReadMzml(string path) {
        var options = RawDataAccessOptions.Mzml;
        options.Log ??= RawDataAccessOptions.Log;
        return new MzmlReader(options).ReadMzml(path, FileID, _isGuiProcess, _bgWorker);
    }

    private RawMeasurement? ApplyRtCorrection(RawMeasurement? measurement) {
        if (_correctedRtList == null || measurement == null) return measurement;
        if (_correctedRtList.Count != measurement.SpectrumList.Count) return measurement;
        for (var i = 0; i < _correctedRtList.Count; i++) {
            measurement.SpectrumList[i].ScanStartTime = _correctedRtList[i];
        }
        return measurement;
    }

    // ------------------------------------------------------------------
    // Ion mobility / imaging helpers (delegated to the legacy dll when present)
    // ------------------------------------------------------------------

    public RawCalibrationInfo? ReadIonmobilityCalibrationInfo() {
        var legacy = LegacyReaderPlugin.Default;
        if (legacy == null || Extension == RawDataExtension.mzml) return null;
        try {
            return legacy.ReadIonmobilityCalibrationInfo(Filepath, FileID);
        }
        catch (Exception ex) {
            RawDataAccessOptions.Log("Error: " + ex.Message);
            return null;
        }
    }

    public MaldiFrameLaserInfo GetMaldiFrameLaserInfo() {
        var legacy = LegacyReaderPlugin.Default ?? throw new NotSupportedException("MALDI frame information requires the legacy RawDataHandler dll.");
        return legacy.GetMaldiFrameLaserInfo(Filepath, FileID, _isGuiProcess) ?? throw new NotSupportedException();
    }

    public List<MaldiFrameInfo> GetMaldiFrames() {
        var legacy = LegacyReaderPlugin.Default ?? throw new NotSupportedException("MALDI frame information requires the legacy RawDataHandler dll.");
        return legacy.GetMaldiFrames(Filepath, FileID, _isGuiProcess) ?? throw new NotSupportedException();
    }

    private LegacyReaderPlugin.LegacyAccessHandle LegacyHandle() {
        if (_legacyHandle != null) return _legacyHandle;
        var legacy = LegacyReaderPlugin.Default ?? throw new NotSupportedException("Imaging pixel data requires the legacy RawDataHandler dll (imzML) or a plugin.");
        return _legacyHandle = legacy.Open(Filepath, FileID, _getProfileData, _isImagingMsData, _isGuiProcess, PeakCutOff, MzToleranceForPixelData, DriftToleranceForPixelData);
    }

    [Obsolete("Use asynchronous version")]
    public void SaveRawPixelFeatures(List<Raw2DElement> targetFeatures, List<MaldiFrameInfo> targetFrames) {
        LegacyHandle().Call("SaveRawPixelFeatures", targetFeatures, targetFrames);
    }

    [Obsolete("Use asynchronous version")]
    public RawSpectraOnPixels GetRawPixelFeatures(List<Raw2DElement> targetFeatures, List<MaldiFrameInfo> targetFrames, bool isNewProcess = false) {
        return (RawSpectraOnPixels)LegacyHandle().Call("GetRawPixelFeatures", targetFeatures, targetFrames, isNewProcess)!;
    }

    [Obsolete("Use asynchronous version")]
    public RawSpectraOnPixels GetRawPixelFeature(List<Raw2DElement> targetFeatures, int featureIndex, List<MaldiFrameInfo> targetFrames, bool isNewProcess = false) {
        return (RawSpectraOnPixels)LegacyHandle().Call("GetRawPixelFeature", targetFeatures, featureIndex, targetFrames, isNewProcess)!;
    }

    public Task SaveRawPixelFeaturesAsync(List<Raw2DElement> targetFeatures, List<MaldiFrameInfo> targetFrames, CancellationToken token = default) {
        return (Task)LegacyHandle().Call("SaveRawPixelFeaturesAsync", targetFeatures, targetFrames, token)!;
    }

    public Task<RawSpectraOnPixels> GetRawPixelFeaturesAsync(List<Raw2DElement> targetFeatures, List<MaldiFrameInfo> targetFrames, bool isNewProcess = false, CancellationToken token = default) {
        return (Task<RawSpectraOnPixels>)LegacyHandle().Call("GetRawPixelFeaturesAsync", targetFeatures, targetFrames, isNewProcess, token)!;
    }

    public Task<RawSpectraOnPixels> GetRawPixelFeatureAsync(List<Raw2DElement> targetFeatures, int featureIndex, List<MaldiFrameInfo> targetFrames, bool isNewProcess = false, CancellationToken token = default) {
        return (Task<RawSpectraOnPixels>)LegacyHandle().Call("GetRawPixelFeatureAsync", targetFeatures, featureIndex, targetFrames, isNewProcess, token)!;
    }

    // ------------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------------

    public (Dictionary<int, int>, Dictionary<int, int>) ParseRawdataForStatistics(IEnumerable<RawSpectrum>? peaks) {
        var ms1 = new Dictionary<int, int>();
        var ms2 = new Dictionary<int, int>();
        for (var i = 0; i <= 40; i++) { ms1[i] = 0; ms2[i] = 0; }
        foreach (var spectrum in peaks ?? Array.Empty<RawSpectrum>()) {
            var target = spectrum.MsLevel < 2 ? ms1 : ms2;
            foreach (var peak in spectrum.Spectrum ?? Array.Empty<RawPeakElement>()) {
                if (peak.Intensity <= 0) continue;
                var bin = (int)Math.Floor(Math.Log(peak.Intensity, 2.0));
                target[Math.Min(40, Math.Max(0, bin))]++;
            }
        }
        return (ms1, ms2);
    }

    public void ConvertToIABF(string outputDir = "") {
        throw new NotSupportedException("IABF writing is not implemented in OpenDIAL.RawData; use the original RawDataConverter on Windows.");
    }

    public void DataDump(string filepath) {
        using var access = new RawDataAccess(filepath, 0, false, false, false);
        var measurement = access.GetMeasurement();
        if (measurement == null) return;
        Console.WriteLine(measurement.Method);
        foreach (var s in measurement.SpectrumList) {
            Console.WriteLine("ID {0}, RT {1}, DT {2}, MS level {3}, Spec count {4}, CE {5}, Pre {6}, HiP {7}, LoP {8}",
                s.Index, s.ScanStartTime, s.DriftTime, s.MsLevel, s.Spectrum?.Length ?? 0, s.CollisionEnergy,
                s.Precursor?.IsolationTargetMz ?? 0.0, s.Precursor?.IsolationWindowUpperOffset ?? 0.0, s.Precursor?.IsolationWindowLowerOffset ?? 0.0);
        }
    }

    public void MzMlRawFileDump(string filepath) => DataDump(filepath);

    public void RawDataStatisticsDump(string file, string output) {
        using var access = new RawDataAccess(file, 0, false, false, false);
        var spectra = access.GetMeasurement()?.SpectrumList ?? new List<RawSpectrum>();
        var (ms1, ms2) = ParseRawdataForStatistics(spectra);
        using var writer = new StreamWriter(output, false);
        writer.WriteLine("MSlevel=1");
        for (var i = 0; i <= 40; i++) writer.WriteLine(i + "\t" + ms1[i]);
        writer.WriteLine("MSlevel=2");
        for (var i = 0; i <= 40; i++) writer.WriteLine(i + "\t" + ms2[i]);
    }

    ~RawDataAccess() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing) {
        if (_disposed) return;
        _disposed = true;
        if (isDisposing) {
            _legacyHandle?.Dispose();
            _legacyHandle = null;
        }
    }
}
