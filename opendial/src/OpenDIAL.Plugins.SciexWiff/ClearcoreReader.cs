using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Clearcore2.Data;
using Clearcore2.Data.AnalystDataProvider;
using Clearcore2.Data.DataAccess.SampleData;
using Clearcore2.Utility;
using CompMs.Common.Algorithm.PeakPick;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using OpenDIAL.RawData.Plugins;

namespace OpenDIAL.Plugins.SciexWiff;

/// <summary>
/// The Clearcore2 side of the plugin (compiled only when the SDK assemblies are present).
///
/// Mapping, following OpenQuant / alpharaw:
///   batch -> sample -> experiment (one entry of the acquisition method: TOF MS, TOF MSMS / product
///   ion, SWATH window, MRM ...) -> cycle. One RawSpectrum per (experiment, cycle); the retention
///   time of a cycle is the experiment's own (GetRTFromExperimentCycle); ExperimentID is the
///   experiment index so MS-DIAL can group SWATH windows / DIA channels; IDA (DDA) dependent
///   scans that were not triggered are skipped; profile spectra are centroided with the same
///   local-maximum method MS-DIAL applies to profile data.
/// </summary>
internal static class ClearcoreReader
{
    private static readonly object InitLock = new();
    private static bool _initialised;

    private static void EnsureInitialised() {
        lock (InitLock) {
            if (_initialised) return;
            // Clearcore2 parses numbers with the thread culture; pin it (pt-BR would break masses).
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                UseManagedStructuredStorage();
            }
            _initialised = true;
        }
    }

    /// <summary>
    /// Clearcore2.StructuredStorage chooses between the Windows COM structured-storage API and a
    /// managed OpenMcdf implementation through a private static flag that is only false under
    /// Mono. On .NET 8 / macOS / Linux the COM path cannot work, so the flag is forced off.
    /// </summary>
    private static void UseManagedStructuredStorage() {
        var dir = Path.GetDirectoryName(typeof(AnalystDataProviderFactory).Assembly.Location)!;
        var path = Path.Combine(dir, "Clearcore2.StructuredStorage.dll");
        if (!File.Exists(path)) throw new FileNotFoundException("Clearcore2.StructuredStorage.dll is missing next to the plugin", path);
        var asm = Assembly.LoadFrom(path);
        var type = asm.GetType("Clearcore2.StructuredStorage.StgStorage", throwOnError: true)!;
        var field = type.GetField("sWindows", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Unexpected Clearcore2.StructuredStorage layout (field sWindows not found)");
        field.SetValue(null, false);
    }


    private static bool _vendorCentroidWarned;

    /// <summary>
    /// SCIEX's spectral peak finder for one cycle. PeakClass carries the fitted apex (apexX/apexY)
    /// as well as the peak's area and half-height bounds; MS-DIAL only wants m/z and intensity.
    /// Returns null when the SDK cannot run it, so the caller falls back to local-maximum centroids.
    /// </summary>
    private static RawPeakElement[]? TryVendorCentroids(MSExperiment exp, int cycle, double minIntensity, Action<string> log) {
        try {
            var found = exp.GetPeakArray(cycle);
            if (found is null || found.Length == 0) return null;
            var peaks = new List<RawPeakElement>(found.Length);
            foreach (var p in found) {
                if (p is null || p.apexY <= minIntensity || p.apexX <= 0) continue;
                peaks.Add(new RawPeakElement { Mz = p.apexX, Intensity = p.apexY });
            }
            if (peaks.Count == 0) return null;
            peaks.Sort((a, b) => a.Mz.CompareTo(b.Mz));
            return peaks.ToArray();
        }
        catch (Exception ex) {
            if (!_vendorCentroidWarned) {
                _vendorCentroidWarned = true;
                log($"[wiff] the SCIEX spectral peak finder is unavailable ({ex.GetType().Name}: {ex.Message}); falling back to MS-DIAL's local-maximum centroiding");
            }
            return null;
        }
    }

    public static IReadOnlyList<string> ListSamples(string path) {
        EnsureInitialised();
        var provider = new AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared);
        try {
            var batch = AnalystDataProviderFactory.CreateBatch(Path.GetFullPath(path), provider);
            return batch.GetSampleNames();
        }
        finally {
            try { provider.Close(); } catch { }
        }
    }

    public static RawMeasurement Read(string path, int fileId, RawReadOptions options) {
        EnsureInitialised();
        var log = options.Log ?? (_ => { });
        // How profile spectra become centroids. "sciex" (the default) uses the vendor's own spectral
        // peak finder, the one Analyst and the closed MS-DIAL reader use: it fits each profile peak,
        // so its apex sits about a quarter above the highest sampled point and its peak list is
        // cleaner. "msdial" applies MS-DIAL's generic local-maximum method instead, and "0" keeps the
        // profile data. Heights feed the minimum-peak-height cut-off, so this choice moves results.
        var centroidMode = (Environment.GetEnvironmentVariable("OPENDIAL_WIFF_CENTROID") ?? "sciex").Trim().ToLowerInvariant();
        if (options.GetProfileData) centroidMode = "0";
        var centroid = centroidMode is not ("0" or "off" or "false" or "profile");
        var vendorCentroid = centroid && centroidMode is not ("msdial" or "localmax");
        var minIntensity = double.TryParse(Environment.GetEnvironmentVariable("OPENDIAL_WIFF_MIN_INTENSITY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mi) ? mi : 0.0;
        var full = Path.GetFullPath(path);
        var provider = new AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared);
        try {
            var batch = AnalystDataProviderFactory.CreateBatch(full, provider);
            var names = batch.GetSampleNames();
            var sampleIndex = SciexWiffReaderPlugin.ResolveSampleIndex(full, names);
            var sample = batch.GetSample(sampleIndex);
            if (!sample.HasMassSpectrometerData) throw new InvalidDataException($"{Path.GetFileName(path)}: sample '{names[sampleIndex]}' has no mass spectrometer data");
            var ms = sample.MassSpectrometerSample;
            var experimentCount = ms.ExperimentCount;
            log($"[wiff] {Path.GetFileName(path)}: sample {sampleIndex + 1}/{names.Length} '{names[sampleIndex]}', {experimentCount} experiment(s), instrument {ms.InstrumentName}");

            var spectra = new List<RawSpectrum>();
            var anySwath = false;
            var anyIda = ms.HasIDAData;
            var totalScans = 0L;
            var experiments = new MSExperiment[experimentCount];
            for (var e = 0; e < experimentCount; e++) {
                experiments[e] = ms.GetMSExperiment(e);
                totalScans += experiments[e].Details.NumberOfScans;
            }
            var done = 0L;
            var lastPercent = -1;
            for (var e = 0; e < experimentCount; e++) {
                var exp = experiments[e];
                var details = exp.Details;
                var isSwath = details.IsSwath;
                anySwath |= isSwath;
                var polarity = details.Polarity switch {
                    MSExperimentInfo.PolarityEnum.Positive => ScanPolarity.Positive,
                    MSExperimentInfo.PolarityEnum.Negative => ScanPolarity.Negative,
                    _ => ScanPolarity.Undefined,
                };
                var isMrmLike = details.ExperimentType == ExperimentType.MRM || details.ExperimentType == ExperimentType.SIM;
                // In IDA — SCIEX's DDA — experiment 0 is the survey (TOF MS) and every later experiment
                // is a dependent product-ion scan whose precursor is chosen per cycle from the n most
                // intense survey ions. Those experiments still carry a FragmentBasedScanMassRange, but
                // its FixedMasses is only the placeholder written by the acquisition method: the same
                // value in every dependent experiment. Reading it would give every channel one identical
                // precursor. The precursor that was actually isolated is on each spectrum (ParentMZ).
                var isIdaDependent = details.IDAType is MSExperimentInfo.IDAExperimentType.Dependent
                                                     or MSExperimentInfo.IDAExperimentType.Confirmation;
                // fixed precursor / isolation window of the experiment (SWATH, fixed product ion scans)
                double fixedPrecursor = 0, fixedWindow = 0;
                if (details.MassRangeInfo is { Length: > 0 } && details.MassRangeInfo[0] is FragmentBasedScanMassRange fr) {
                    if (!isIdaDependent && fr.FixedMasses is { Length: > 0 }) fixedPrecursor = fr.FixedMasses[0];
                    fixedWindow = fr.IsolationWindow;
                }
                var scans = details.NumberOfScans;
                for (var cycle = 0; cycle < scans; cycle++) {
                    done++;
                    MassSpectrum spectrum;
                    MassSpectrumInfo info;
                    try {
                        spectrum = exp.GetMassSpectrum(cycle);
                        info = exp.GetMassSpectrumInfo(cycle);
                    }
                    catch (Exception ex) {
                        log($"[wiff] experiment {e} cycle {cycle}: {ex.Message}");
                        continue;
                    }
                    var msLevel = Math.Max(1, info.MSLevel);
                    var n = spectrum.NumDataPoints;
                    if (msLevel > 1 && !isSwath && n <= 0) {
                        continue; // untriggered IDA dependent scan
                    }
                    // a dependent scan with no precursor recorded was never triggered either
                    if (isIdaDependent && info.ParentMZ <= 0) {
                        continue;
                    }
                    RawPeakElement[] peaks;
                    var representation = info.CentroidMode ? SpectrumRepresentation.Centroid : SpectrumRepresentation.Profile;
                    var vendorPeaks = n > 0 && vendorCentroid && !info.CentroidMode && !isMrmLike
                        ? TryVendorCentroids(exp, cycle, minIntensity, log)
                        : null;
                    if (vendorPeaks is not null) {
                        peaks = vendorPeaks;
                        representation = SpectrumRepresentation.Centroid;
                    }
                    else {
                        double[] xs, ys;
                        if (n > 0) {
                            if (centroid && !info.CentroidMode) {
                                // restore the zero points SCIEX strips from profile data so that peaks are bounded
                                try { exp.AddZeros(spectrum, 1); } catch { }
                            }
                            xs = spectrum.GetActualXValues();
                            ys = spectrum.GetActualYValues();
                        }
                        else {
                            xs = Array.Empty<double>();
                            ys = Array.Empty<double>();
                        }
                        peaks = new RawPeakElement[xs.Length];
                        for (var i = 0; i < xs.Length; i++) {
                            peaks[i].Mz = xs[i];
                            peaks[i].Intensity = ys[i];
                        }
                        if (centroid && !info.CentroidMode && peaks.Length > 0 && !isMrmLike) {
                            peaks = SpectralCentroiding.CentroidByLocalMaximumMethod(peaks, absThreshold: minIntensity);
                            representation = SpectrumRepresentation.Centroid;
                        }
                    }
                    var rt = exp.GetRTFromExperimentCycle(cycle);
                    var s = new RawSpectrum {
                        Id = $"exp={e} cycle={cycle}",
                        ScanNumber = cycle,
                        ExperimentID = e,
                        MsLevel = msLevel,
                        ScanPolarity = polarity,
                        SpectrumRepresentation = representation,
                        ScanStartTime = rt,
                        ScanStartTimeUnit = Units.Minute,
                        ScanWindowLowerLimit = details.StartMass,
                        ScanWindowUpperLimit = details.EndMass,
                        Spectrum = peaks,
                    };
                    if (msLevel > 1) {
                        var center = fixedPrecursor > 0 ? fixedPrecursor : info.ParentMZ;
                        var window = fixedWindow > 0 ? fixedWindow : (isSwath ? 0.0 : 1.0);
                        if (window <= 0) window = 3.0;
                        s.Precursor = new RawPrecursorIon {
                            SelectedIonMz = center,
                            IsolationTargetMz = center,
                            IsolationWindowLowerOffset = window / 2.0,
                            IsolationWindowUpperOffset = window / 2.0,
                            Dissociationmethod = DissociationMethods.CID,
                            CollisionEnergy = info.CollisionEnergy,
                            CollisionEnergyUnit = Units.ElectronVolt,
                        };
                        s.CollisionEnergy = info.CollisionEnergy;
                    }
                    SpectrumParser.setSpectrumProperties(s);
                    if (peaks.Length == 0) {
                        s.LowestObservedMz = details.StartMass;
                        s.HighestObservedMz = details.EndMass;
                        s.MinIntensity = 0;
                    }
                    spectra.Add(s);
                    var percent = (int)(100.0 * done / Math.Max(1, totalScans));
                    if (percent != lastPercent && percent % 10 == 0) {
                        lastPercent = percent;
                        options.Progress?.Report(percent);
                    }
                }
            }
            var measurement = SpectrumParser.GetRawMeasurementObj(full, fileId, spectra);
            measurement.Sample = new RawSample { Id = fileId.ToString(), Name = names[sampleIndex], InstrumentType = ms.InstrumentName, IonAnalyzerType = "TOF" };
            measurement.SourceFileInfo = new RawSourceFileInfo { Id = fileId.ToString(), Name = Path.GetFileNameWithoutExtension(full), Location = full };
            measurement.Method = anySwath ? MeasurmentMethod.SWATH : anyIda ? MeasurmentMethod.DDA : MeasurmentMethod.DDA;
            measurement.AccumulatedSpectrumList ??= new List<RawSpectrum>();
            measurement.ChromatogramList ??= new List<RawChromatogram>();
            log($"[wiff] {spectra.Count} spectra ({spectra.Count(x => x.MsLevel == 1)} MS1, {spectra.Count(x => x.MsLevel > 1)} MSn); method {measurement.Method}");
            return measurement;
        }
        finally {
            try { provider.Close(); } catch { }
        }
    }
}
