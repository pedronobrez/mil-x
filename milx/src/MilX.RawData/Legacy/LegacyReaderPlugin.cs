using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using CompMs.Common.DataObj;

namespace MilX.RawData.Legacy;

/// <summary>
/// Optional bridge to the original closed RawDataHandler dll (either the
/// "RawDataHandler-Vendor-UnSupported" build shipped with the MS-DIAL sources or the
/// vendor-enabled "RawDataHandler" build from an official MS-DIAL Windows release).
/// The dll is loaded by reflection so that MIL-X never has a compile-time dependency
/// on it. It is used for formats MIL-X does not implement natively (ABF, IBF, NetCDF,
/// imzML) and, on Windows, for vendor formats when the vendor-enabled build is present.
///
/// Search order: $MILX_LEGACY_RAWDATA_DLL, then &lt;app&gt;/plugins/legacy/*.dll,
/// then &lt;app&gt;/RawDataHandler.dll and &lt;app&gt;/RawDataHandler-Vendor-UnSupported.dll.
/// </summary>
public sealed class LegacyReaderPlugin
{
    private static readonly object Sync = new();
    private static LegacyReaderPlugin? _default;
    private static bool _probed;

    private readonly Type _accessType;
    private readonly ConstructorInfo _ctor;

    public string AssemblyPath { get; }
    public string AssemblyName { get; }

    private LegacyReaderPlugin(string path, Type accessType, ConstructorInfo ctor) {
        AssemblyPath = path;
        _accessType = accessType;
        _ctor = ctor;
        AssemblyName = accessType.Assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>The first usable legacy dll, or null when none is present.</summary>
    public static LegacyReaderPlugin? Default {
        get {
            lock (Sync) {
                if (!_probed) {
                    _probed = true;
                    foreach (var candidate in CandidatePaths()) {
                        var plugin = TryLoad(candidate);
                        if (plugin != null) {
                            _default = plugin;
                            break;
                        }
                    }
                }
                return _default;
            }
        }
    }

    public static IEnumerable<string> CandidatePaths() {
        var env = Environment.GetEnvironmentVariable("MILX_LEGACY_RAWDATA_DLL");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) yield return env!;
        var baseDir = AppContext.BaseDirectory;
        var legacyDir = Path.Combine(baseDir, "plugins", "legacy");
        if (Directory.Exists(legacyDir)) {
            foreach (var dll in Directory.GetFiles(legacyDir, "RawDataHandler*.dll")) yield return dll;
        }
        var vendor = Path.Combine(baseDir, "RawDataHandler.dll");
        if (File.Exists(vendor)) yield return vendor;
        var unsupported = Path.Combine(baseDir, "RawDataHandler-Vendor-UnSupported.dll");
        if (File.Exists(unsupported)) yield return unsupported;
    }

    public static LegacyReaderPlugin? TryLoad(string path) {
        try {
            var asm = Assembly.LoadFrom(path);
            var type = asm.GetType("CompMs.RawDataHandler.Core.RawDataAccess", throwOnError: false);
            if (type == null) return null;
            var ctor = type.GetConstructor(new[] { typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(List<double>), typeof(BackgroundWorker) });
            if (ctor == null) return null;
            return new LegacyReaderPlugin(path, type, ctor);
        }
        catch {
            return null;
        }
    }

    private object CreateAccess(string filepath, int fileId, bool getProfileData, bool isImagingMsData, bool isGuiProcess, List<double>? correctedRts, BackgroundWorker? bgWorker) {
        return _ctor.Invoke(new object?[] { filepath, fileId, getProfileData, isImagingMsData, isGuiProcess, correctedRts, bgWorker });
    }

    private object? Invoke(object access, string method, params object?[] args) {
        var mi = _accessType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        if (mi == null) throw new MissingMethodException(_accessType.FullName, method);
        try {
            return mi.Invoke(access, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null) {
            throw ex.InnerException;
        }
    }

    public RawMeasurement? GetMeasurement(string filepath, int fileId, bool getProfileData, bool isImagingMsData, bool isGuiProcess, List<double>? correctedRts, BackgroundWorker? bgWorker) {
        var access = CreateAccess(filepath, fileId, getProfileData, isImagingMsData, isGuiProcess, correctedRts, bgWorker);
        try {
            return Invoke(access, "GetMeasurement") as RawMeasurement;
        }
        finally {
            (access as IDisposable)?.Dispose();
        }
    }

    public RawCalibrationInfo? ReadIonmobilityCalibrationInfo(string filepath, int fileId) {
        var access = CreateAccess(filepath, fileId, false, false, false, null, null);
        try {
            return Invoke(access, "ReadIonmobilityCalibrationInfo") as RawCalibrationInfo;
        }
        finally {
            (access as IDisposable)?.Dispose();
        }
    }

    public List<MaldiFrameInfo>? GetMaldiFrames(string filepath, int fileId, bool isGuiProcess) {
        var access = CreateAccess(filepath, fileId, false, true, isGuiProcess, null, null);
        try {
            return Invoke(access, "GetMaldiFrames") as List<MaldiFrameInfo>;
        }
        finally {
            (access as IDisposable)?.Dispose();
        }
    }

    public MaldiFrameLaserInfo? GetMaldiFrameLaserInfo(string filepath, int fileId, bool isGuiProcess) {
        var access = CreateAccess(filepath, fileId, false, true, isGuiProcess, null, null);
        try {
            return Invoke(access, "GetMaldiFrameLaserInfo") as MaldiFrameLaserInfo;
        }
        finally {
            (access as IDisposable)?.Dispose();
        }
    }

    /// <summary>Creates a long-lived legacy access object for imaging pixel queries; the caller must dispose it.</summary>
    public LegacyAccessHandle Open(string filepath, int fileId, bool getProfileData, bool isImagingMsData, bool isGuiProcess, double? peakCutOff, double? mzTolerance, double? driftTolerance) {
        var access = CreateAccess(filepath, fileId, getProfileData, isImagingMsData, isGuiProcess, null, null);
        void Set(string name, double? value) {
            if (value == null) return;
            _accessType.GetProperty(name)?.SetValue(access, value.Value);
        }
        Set("PeakCutOff", peakCutOff);
        Set("MzToleranceForPixelData", mzTolerance);
        Set("DriftToleranceForPixelData", driftTolerance);
        return new LegacyAccessHandle(this, access);
    }

    public sealed class LegacyAccessHandle : IDisposable
    {
        private readonly LegacyReaderPlugin _owner;
        private readonly object _access;

        internal LegacyAccessHandle(LegacyReaderPlugin owner, object access) {
            _owner = owner;
            _access = access;
        }

        public object? Call(string method, params object?[] args) => _owner.Invoke(_access, method, args);

        public void Dispose() => (_access as IDisposable)?.Dispose();
    }
}
