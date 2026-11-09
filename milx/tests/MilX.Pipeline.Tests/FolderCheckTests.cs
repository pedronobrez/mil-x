using MilX.Pipeline.Vendor;
using Xunit;

namespace MilX.Pipeline.Tests;

public class FolderCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "milx-folder-" + Guid.NewGuid().ToString("N"));

    public FolderCheckTests() { Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private string Touch(string name) { var p = Path.Combine(_dir, name); File.WriteAllText(p, "x"); return p; }

    [Fact]
    public void A_wiff_without_its_scan_is_named_and_the_stray_is_offered_as_a_guess()
    {
        var whole = Touch("run_mix1.wiff");
        Touch("run_mix1.wiff.scan");
        var broken = Touch("run_mix2.wiff");
        Touch("run.wiff_mix2.scan");   // the companion, renamed by hand
        Touch("._run_mix2.wiff.scan"); // what the Finder leaves on an exFAT stick: never a finding
        var report = FolderCheck.Check(new[] { _dir });

        Assert.Equal(new[] { whole, broken }, report.Paths);
        var missing = Assert.Single(report.OfKind(FolderFindingKind.MissingScan));
        Assert.Equal(broken, missing.Path);
        Assert.Contains("has no run_mix2.wiff.scan beside it", missing.Finding);
        Assert.Contains("the spectra, the extracted ion chromatograms and the quantitation do not", missing.Finding);
        Assert.Contains("run.wiff_mix2.scan", missing.Action);
        var stray = Assert.Single(report.OfKind(FolderFindingKind.StrayScan));
        Assert.True(stray.Renameable);
        Assert.Equal(Path.Combine(_dir, "run.wiff_mix2.scan"), stray.RenameFrom);
        Assert.Equal(broken + ".scan", stray.RenameTo);
        Assert.Contains("a guess from the two names", stray.Action);
        Assert.Equal("2 files to open; 2 things to look at.", report.Summary());

        // the rename repairs it, and never writes over anything
        FolderCheck.ApplyRename(stray);
        Assert.True(File.Exists(broken + ".scan"));
        Assert.False(FolderCheck.Check(new[] { _dir }).HasFindings);
        Touch("other.wiff"); var strayTwo = Touch("other.scan.wiff.scan");
        var again = FolderCheck.Check(new[] { _dir });
        var proposal = Assert.Single(again.Renames);
        File.WriteAllText(proposal.RenameTo, "the right one");
        Assert.Throws<IOException>(() => FolderCheck.ApplyRename(proposal));
        Assert.True(File.Exists(strayTwo));
    }

    [Fact]
    public void Wiff2_files_other_vendors_and_duplicates_are_reported_and_nothing_is_decoded()
    {
        var wiff = Touch("a.wiff"); Touch("a.wiff.scan"); Touch("a.wiff2");
        Touch("lonely.wiff2");
        Touch("brk.tdf");
        var mzml = Touch("b.mzML");
        var report = FolderCheck.Check(new[] { _dir, mzml }, openPaths: new[] { wiff });
        // one per acquisition: a.wiff (open already), b.mzML, lonely.wiff2 (goes to msconvert)
        Assert.Equal(new[] { Path.Combine(_dir, "lonely.wiff2"), mzml }.OrderBy(p => p, StringComparer.Ordinal), report.Paths.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Contains(report.Findings, f => f.Kind == FolderFindingKind.Duplicate && f.Finding == "a.wiff is open already");
        Assert.Contains(report.Findings, f => f.Kind == FolderFindingKind.Duplicate && f.Finding == "b.mzML was given twice");
        var ignored = report.OfKind(FolderFindingKind.Ignored).ToList();
        Assert.Contains(ignored, f => f.Finding.StartsWith("1 .wiff2 file (a.wiff2)") && f.Action.StartsWith("nothing is lost"));
        Assert.Contains(ignored, f => f.Finding.StartsWith("1 .wiff2 file (lonely.wiff2) with no .wiff"));
        Assert.Contains(ignored, f => f.Finding.Contains("brk.tdf") && f.Finding.Contains("Bruker timsTOF"));
    }

    [Fact]
    public void Absent_and_unsupported_paths_are_findings_not_exceptions()
    {
        var report = FolderCheck.Check(new[] { Path.Combine(_dir, "gone.wiff"), Touch("notes.txt") });
        Assert.Empty(report.Paths);
        Assert.Contains(report.Findings, f => f.Kind == FolderFindingKind.Absent && f.Finding == "gone.wiff is not there");
        var unsupported = Assert.Single(report.OfKind(FolderFindingKind.Unsupported));
        Assert.StartsWith("notes.txt is a .txt file, which this build does not read", unsupported.Finding);
        Assert.Contains(".mzml", unsupported.Action);
        Assert.Equal("0 files to open; 2 things to look at.", report.Summary());
    }

    [Fact]
    public void The_owner_guess_is_by_the_names_alone()
    {
        var wiffs = new[] { "/d/name_mix1.wiff", "/d/name_mix2.wiff" };
        // the longer common prefix decides; a tie (name.wiff_mix2.scan shares "name" and ".scan" with both) goes to the first name, as MIL-Q has it
        Assert.Equal("/d/name_mix2.wiff", FolderCheck.GuessOwner("/d/name_mix2.scan", wiffs));
        Assert.Equal("/d/name_mix1.wiff", FolderCheck.GuessOwner("/d/name_mix1_copy.wiff.scan", wiffs));
        Assert.Equal("/d/name_mix1.wiff", FolderCheck.GuessOwner("/d/name.wiff_mix2.scan", wiffs));
        Assert.Equal(string.Empty, FolderCheck.GuessOwner("/d/zzz.scan", wiffs));
    }
}
