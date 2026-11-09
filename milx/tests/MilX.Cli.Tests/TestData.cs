using System.Diagnostics;

namespace MilX.Cli.Tests;

/// <summary>Where the tests find the synthetic study, and how they make the IDA-like variant of it.</summary>
internal static class TestData
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MilX.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("the repository root was not found above " + AppContext.BaseDirectory);
        }
    }

    public static string Synthetic => Path.Combine(Root, "testdata", "synthetic_dda");
    public static string SampleA01 => Path.Combine(Synthetic, "SampleA01.mzML");

    /// <summary>A fresh folder under the temp directory, removed by the caller.</summary>
    public static string Scratch(string tag)
    {
        var path = Path.Combine(Path.GetTempPath(), "milx-cli-tests", tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The IDA-like synthetic file (tools/make_synthetic_mzml.py --ida 4): null when python3 with numpy is
    /// not at hand, in which case the tests that need it say so and pass. Made once per test run.
    /// </summary>
    public static string? IdaFile()
    {
        lock (IdaLock)
        {
            if (_ida is not null) return _ida.Length == 0 ? null : _ida;
            var dir = Path.Combine(Path.GetTempPath(), "milx-cli-tests", "ida-" + Environment.ProcessId);
            Directory.CreateDirectory(dir);
            try
            {
                var psi = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                psi.ArgumentList.Add("-I");
                psi.ArgumentList.Add(Path.Combine(Root, "tools", "make_synthetic_mzml.py"));
                psi.ArgumentList.Add("--out"); psi.ArgumentList.Add(dir);
                psi.ArgumentList.Add("--samples"); psi.ArgumentList.Add("1");
                psi.ArgumentList.Add("--ida"); psi.ArgumentList.Add("4");
                using var p = Process.Start(psi)!;
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                var file = Path.Combine(dir, "SampleA01.mzML");
                _ida = p.ExitCode == 0 && File.Exists(file) ? file : string.Empty;
            }
            catch (Exception) { _ida = string.Empty; }
            return _ida.Length == 0 ? null : _ida;
        }
    }

    private static readonly object IdaLock = new();
    private static string? _ida;
}
