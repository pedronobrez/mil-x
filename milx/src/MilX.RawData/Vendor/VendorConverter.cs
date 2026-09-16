using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MilX.RawData.Vendor;

public enum ConverterKind
{
    None,
    NativeMsconvert,
    DockerMsconvert,
}

public sealed class ConverterAvailability
{
    public ConverterKind Kind { get; init; }
    public string? Executable { get; init; }
    public string? DockerImage { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsAvailable => Kind != ConverterKind.None;
}

/// <summary>Settings for the ProteoWizard msconvert bridge. Every value can also come from an environment variable.</summary>
public sealed class VendorConverterOptions
{
    /// <summary>Path to a native msconvert executable (env MILX_MSCONVERT). "docker" forces the Docker path.</summary>
    public string? MsconvertPath { get; set; } = Environment.GetEnvironmentVariable("MILX_MSCONVERT");

    /// <summary>Docker executable (env MILX_DOCKER, default "docker").</summary>
    public string DockerPath { get; set; } = Environment.GetEnvironmentVariable("MILX_DOCKER") ?? "docker";

    /// <summary>ProteoWizard image (env MILX_PWIZ_IMAGE). The default image bundles the vendor readers under Wine.</summary>
    public string DockerImage { get; set; } = Environment.GetEnvironmentVariable("MILX_PWIZ_IMAGE") ?? "proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest";

    /// <summary>Docker platform flag; the pwiz image is x86-64 only, so Apple Silicon needs emulation (env MILX_DOCKER_PLATFORM).</summary>
    public string DockerPlatform { get; set; } = Environment.GetEnvironmentVariable("MILX_DOCKER_PLATFORM") ?? "linux/amd64";

    /// <summary>Directory for converted mzML files (env MILX_MZML_CACHE). Null = next to the source file.</summary>
    public string? CacheDirectory { get; set; } = Environment.GetEnvironmentVariable("MILX_MZML_CACHE");

    /// <summary>Extra msconvert arguments (env MILX_MSCONVERT_ARGS), appended after the defaults.</summary>
    public string? ExtraArguments { get; set; } = Environment.GetEnvironmentVariable("MILX_MSCONVERT_ARGS");

    /// <summary>Apply vendor peak picking (centroiding) on all MS levels. MS-DIAL expects centroid data by default.</summary>
    public bool VendorPeakPicking { get; set; } = !string.Equals(Environment.GetEnvironmentVariable("MILX_MSCONVERT_PROFILE"), "1", StringComparison.Ordinal);

    /// <summary>Re-convert even when a cached mzML exists.</summary>
    public bool ForceReconvert { get; set; } = string.Equals(Environment.GetEnvironmentVariable("MILX_MSCONVERT_FORCE"), "1", StringComparison.Ordinal);

    public Action<string>? Log { get; set; }
}

/// <summary>
/// Bridge that turns vendor raw files (Thermo .raw, Agilent/Bruker .d, Sciex .wiff/.wiff2,
/// Shimadzu .lcd/.qgd, Waters .raw directories ...) into mzML using ProteoWizard's msconvert,
/// either a native executable or the official Docker image, and caches the result.
/// </summary>
public sealed class VendorConverter
{
    public static VendorConverter Default { get; set; } = new VendorConverter(new VendorConverterOptions());

    private static readonly HashSet<string> VendorExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".raw", ".d", ".wiff", ".wiff2", ".lcd", ".qgd", ".lrp", ".baf", ".tdf", ".tsf", ".yep", ".mzxml", ".mgf",
    };

    private readonly VendorConverterOptions _options;
    private readonly object _sync = new();
    private readonly Dictionary<string, Task<string>> _inflight = new(StringComparer.OrdinalIgnoreCase);

    public VendorConverter(VendorConverterOptions options) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public VendorConverterOptions Options => _options;

    public static bool IsVendorPath(string path) {
        if (string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path);
        if (!VendorExtensions.Contains(ext)) return false;
        if (string.Equals(ext, ".mzxml", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".mgf", StringComparison.OrdinalIgnoreCase)) return true;
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>Returns the mzML path that should be read for <paramref name="path"/> (cached or freshly converted).</summary>
    public string EnsureMzml(string path, CancellationToken cancellationToken = default) {
        return EnsureMzmlAsync(path, cancellationToken).GetAwaiter().GetResult();
    }

    public Task<string> EnsureMzmlAsync(string path, CancellationToken cancellationToken = default) {
        var full = Path.GetFullPath(path);
        lock (_sync) {
            if (_inflight.TryGetValue(full, out var existing)) {
                return existing;
            }
            var task = Task.Run(() => EnsureMzmlCore(full, cancellationToken), cancellationToken);
            _inflight[full] = task;
            task.ContinueWith(_ => { lock (_sync) { _inflight.Remove(full); } }, TaskScheduler.Default);
            return task;
        }
    }

    public string GetCachedMzmlPath(string vendorPath) {
        var full = Path.GetFullPath(vendorPath);
        var name = Path.GetFileNameWithoutExtension(full.TrimEnd(Path.DirectorySeparatorChar));
        var dir = _options.CacheDirectory;
        if (string.IsNullOrEmpty(dir)) {
            dir = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();
        }
        return Path.Combine(dir!, name + ".mzML");
    }

    private string EnsureMzmlCore(string full, CancellationToken cancellationToken) {
        if (!File.Exists(full) && !Directory.Exists(full)) {
            throw new FileNotFoundException("Vendor raw data not found", full);
        }
        var target = GetCachedMzmlPath(full);
        if (!_options.ForceReconvert && File.Exists(target) && new FileInfo(target).Length > 0 && IsUpToDate(full, target)) {
            Log($"[vendor] using cached mzML: {target}");
            return target;
        }
        var availability = Detect();
        if (!availability.IsAvailable) {
            throw new NotSupportedException(
                $"Cannot read '{Path.GetFileName(full)}' on this platform: vendor formats need ProteoWizard msconvert. {availability.Message}");
        }
        Log($"[vendor] converting {full} -> {target} via {availability.Kind}");
        var dir = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(dir);
        var tmpName = Path.GetFileNameWithoutExtension(target) + ".converting.mzML";
        var tmpPath = Path.Combine(dir, tmpName);
        if (File.Exists(tmpPath)) File.Delete(tmpPath);
        var exit = availability.Kind == ConverterKind.NativeMsconvert
            ? RunNative(availability.Executable!, full, dir, tmpName, cancellationToken)
            : RunDocker(availability.DockerImage!, full, dir, tmpName, cancellationToken);
        if (exit != 0 || !File.Exists(tmpPath) || new FileInfo(tmpPath).Length == 0) {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw new InvalidOperationException($"msconvert failed for '{full}' (exit code {exit}). See log for details.");
        }
        if (File.Exists(target)) File.Delete(target);
        File.Move(tmpPath, target);
        Log($"[vendor] conversion finished: {target}");
        return target;
    }

    private static bool IsUpToDate(string source, string target) {
        try {
            var t = File.GetLastWriteTimeUtc(target);
            var s = Directory.Exists(source) ? Directory.GetLastWriteTimeUtc(source) : File.GetLastWriteTimeUtc(source);
            return t >= s;
        }
        catch {
            return true;
        }
    }

    public ConverterAvailability Detect() {
        var configured = _options.MsconvertPath;
        if (!string.IsNullOrWhiteSpace(configured) && !string.Equals(configured, "docker", StringComparison.OrdinalIgnoreCase)) {
            if (File.Exists(configured)) {
                return new ConverterAvailability { Kind = ConverterKind.NativeMsconvert, Executable = configured, Message = "configured msconvert" };
            }
            var onPath = FindOnPath(configured!);
            if (onPath != null) {
                return new ConverterAvailability { Kind = ConverterKind.NativeMsconvert, Executable = onPath, Message = "msconvert on PATH" };
            }
        }
        if (!string.Equals(configured, "docker", StringComparison.OrdinalIgnoreCase)) {
            var native = FindOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "msconvert.exe" : "msconvert");
            if (native != null) {
                return new ConverterAvailability { Kind = ConverterKind.NativeMsconvert, Executable = native, Message = "msconvert on PATH" };
            }
        }
        var docker = FindOnPath(_options.DockerPath) ?? (File.Exists(_options.DockerPath) ? _options.DockerPath : null);
        if (docker != null && DockerDaemonIsUp(docker)) {
            return new ConverterAvailability { Kind = ConverterKind.DockerMsconvert, Executable = docker, DockerImage = _options.DockerImage, Message = "docker + " + _options.DockerImage };
        }
        var help = new StringBuilder();
        help.Append("No msconvert found. Options: (1) install ProteoWizard and put msconvert on PATH or set MILX_MSCONVERT; ");
        help.Append("(2) run a Docker daemon (Docker Desktop, or colima with an x86-64 QEMU VM on Apple Silicon: Rosetta cannot run the image's Wine) and pull '");
        help.Append(_options.DockerImage).Append("'; (3) convert the files to mzML on a Windows machine with msconvert.");
        if (docker != null) help.Append(" Docker was found but the daemon is not running.");
        return new ConverterAvailability { Kind = ConverterKind.None, Message = help.ToString() };
    }

    private bool DockerDaemonIsUp(string docker) {
        try {
            var psi = new ProcessStartInfo(docker, "info") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch {
            return false;
        }
    }

    private static string? FindOnPath(string exe) {
        if (string.IsNullOrEmpty(exe)) return null;
        if (Path.IsPathRooted(exe)) return File.Exists(exe) ? exe : null;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extras = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? new[] { "/opt/homebrew/bin", "/usr/local/bin", "/Applications/Docker.app/Contents/Resources/bin" }
            : Array.Empty<string>();
        foreach (var dir in Split(path, extras)) {
            try {
                var candidate = Path.Combine(dir, exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static IEnumerable<string> Split(string path, string[] extras) {
        foreach (var d in path.Split(Path.PathSeparator)) {
            if (!string.IsNullOrWhiteSpace(d)) yield return d;
        }
        foreach (var d in extras) yield return d;
    }

    private string BuildFilterArguments() {
        var sb = new StringBuilder();
        sb.Append("--mzML --64 --zlib ");
        if (_options.VendorPeakPicking) {
            sb.Append("--filter \"peakPicking vendor msLevel=1-\" ");
        }
        if (!string.IsNullOrWhiteSpace(_options.ExtraArguments)) {
            sb.Append(_options.ExtraArguments).Append(' ');
        }
        return sb.ToString();
    }

    private int RunNative(string exe, string source, string outDir, string outName, CancellationToken token) {
        var args = $"\"{source}\" {BuildFilterArguments()}-o \"{outDir}\" --outfile \"{outName}\"";
        return RunProcess(exe, args, token);
    }

    private int RunDocker(string image, string source, string outDir, string outName, CancellationToken token) {
        var srcDir = Path.GetDirectoryName(source.TrimEnd(Path.DirectorySeparatorChar))!;
        var srcName = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
        var platform = string.IsNullOrWhiteSpace(_options.DockerPlatform) ? string.Empty : $"--platform {_options.DockerPlatform} ";
        var args = $"run --rm {platform}-e WINEDEBUG=-all -v \"{srcDir}:/data\" -v \"{outDir}:/out\" {image} wine msconvert \"/data/{srcName}\" {BuildFilterArguments()}-o /out --outfile \"{outName}\"";
        return RunProcess(_options.DockerPath, args, token);
    }

    private int RunProcess(string exe, string args, CancellationToken token) {
        Log($"[vendor] $ {exe} {args}");
        var psi = new ProcessStartInfo(exe, args) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log("[msconvert] " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log("[msconvert] " + e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var reg = token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } });
        p.WaitForExit();
        token.ThrowIfCancellationRequested();
        return p.ExitCode;
    }

    private void Log(string message) {
        _options.Log?.Invoke(message);
        if (_options.Log == null) Console.WriteLine(message);
    }
}
