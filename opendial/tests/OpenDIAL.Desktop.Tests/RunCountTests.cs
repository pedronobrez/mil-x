using OpenDIAL.Desktop.ViewModels;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// What a run reports about itself has to describe the whole run, not the part of it the log still
/// holds. A batch of eight .wiff files that were all read natively reported none, because the log
/// keeps its last few thousand lines and the reader speaks at the very beginning.
/// </summary>
public class RunCountTests
{
    [Fact]
    public void The_counts_survive_a_log_long_enough_to_wrap()
    {
        var run = new RunViewModel();
        var append = typeof(RunViewModel).GetMethod("Append",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        for (var i = 0; i < 8; i++)
        {
            append.Invoke(run, new object[] { $"[reader] file{i}.wiff: read natively by a raw-file plugin, no conversion needed." });
        }
        append.Invoke(run, new object[] { "Converting vendor format to mzML" });
        for (var i = 0; i < 6000; i++) append.Invoke(run, new object[] { $"Alignment progress: {i % 100} %" });

        Assert.True(run.LogLines.Count <= 5000, $"the log kept {run.LogLines.Count} lines");
        Assert.DoesNotContain(run.LogLines, l => l.Contains("read natively", StringComparison.Ordinal));
        Assert.Equal(8, run.NativeReads);
        Assert.Equal(1, run.Conversions);
    }
}
