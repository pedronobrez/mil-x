namespace MilX.Pipeline.Vendor;

/// <summary>What kind of thing a <see cref="FolderFinding"/> is, for the message line and the tests.</summary>
public enum FolderFindingKind
{
    /// <summary>A .wiff with no .wiff.scan beside it.</summary>
    MissingScan,
    /// <summary>A .scan belonging to no .wiff in its folder.</summary>
    StrayScan,
    /// <summary>A file of a format this build does not read, found beside ones it does.</summary>
    Ignored,
    /// <summary>A file named outright whose extension has no reader.</summary>
    Unsupported,
    /// <summary>A path already in the batch, or given twice.</summary>
    Duplicate,
    /// <summary>A file or folder that cannot be read.</summary>
    Unreadable,
    /// <summary>A path that is not there at all.</summary>
    Absent,
}

/// <summary>One thing worth saying about a path, with what to do about it; a rename when one would repair it.</summary>
public sealed record FolderFinding(string Path, FolderFindingKind Kind, string Finding, string Action, string RenameFrom = "", string RenameTo = "")
{
    public string Name => System.IO.Path.GetFileName(Path) is { Length: > 0 } n ? n : Path;
    public bool Renameable => RenameFrom.Length > 0 && RenameTo.Length > 0;
    public override string ToString() => $"{Finding} — {Action}";
}

/// <summary>What was asked for, what would be opened, and what to look at first.</summary>
public sealed class FolderReport
{
    public List<string> Requested { get; } = new();
    /// <summary>The files that would be opened, in the order they would be.</summary>
    public List<string> Paths { get; } = new();
    public List<FolderFinding> Findings { get; } = new();
    /// <summary>The folders looked in.</summary>
    public List<string> Folders { get; } = new();

    public bool HasFindings => Findings.Count > 0;
    public IEnumerable<FolderFinding> OfKind(FolderFindingKind kind) => Findings.Where(f => f.Kind == kind);
    public IEnumerable<FolderFinding> Renames => Findings.Where(f => f.Renameable);

    public string Summary()
    {
        var files = $"{Paths.Count} file{(Paths.Count == 1 ? string.Empty : "s")}";
        if (Findings.Count == 0) return $"{files} to open; nothing to report.";
        return $"{files} to open; {Findings.Count} thing{(Findings.Count == 1 ? string.Empty : "s")} to look at.";
    }
}

/// <summary>
/// What is about to be opened, looked at before anything opens it. Most of what goes wrong with a folder
/// of acquisitions is visible in the names: a <c>.wiff</c> whose <c>.wiff.scan</c> is not beside it opens
/// and draws its chromatograms and then every spectrum fails; a <c>.wiff2</c> looks like data and holds
/// none; a file picked twice is one injection twice. None of that needs a reader, so this reads names,
/// sizes nothing and decodes nothing — it touches the file system through listing, existence and access
/// only. The one repair it proposes is a rename, because the ordinary way a SCIEX pair comes apart is a
/// companion renamed by hand, and which stray belongs to which <c>.wiff</c> cannot be known from outside
/// the files, so the pairing is by the names and is said to be a guess. Ported from MIL-Q's folder.py;
/// the messages are MIL-Q's.
/// </summary>
public static class FolderCheck
{
    /// <summary>Extensions of other instrument formats this build does not read itself; msconvert writes mzML from all of them.</summary>
    public static readonly IReadOnlyDictionary<string, string> OtherFormats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".tdf"] = "Bruker timsTOF", [".tsf"] = "Bruker timsTOF", [".baf"] = "Bruker", [".yep"] = "Bruker",
        [".mzxml"] = "mzXML", [".mzdata"] = "mzData",
    };

    /// <summary>The companion a .wiff needs: the same path with .scan appended.</summary>
    public static string ScanFileOf(string wiffPath) => wiffPath + ".scan";

    /// <summary>
    /// Looks over the paths about to be opened. <paramref name="paths"/> are files or folders (a folder
    /// contributes the supported files directly inside it); <paramref name="openPaths"/> are the files
    /// already in the batch, so adding one twice is a finding rather than a second copy of every sample.
    /// Folder-wide findings (other formats beside the data, .wiff2 files) are raised for a folder that was
    /// given; a stray .scan is raised either way once a .wiff in its folder is missing its companion.
    /// </summary>
    public static FolderReport Check(IEnumerable<string> paths, IEnumerable<string>? openPaths = null)
    {
        var report = new FolderReport();
        report.Requested.AddRange(paths.Select(p => p.ToString()));
        var already = new HashSet<string>((openPaths ?? Array.Empty<string>()).Select(Key), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<string>();
        var givenFolders = new List<string>();
        var folders = new List<string>();

        foreach (var given in report.Requested)
        {
            var path = Path.GetFullPath(given);
            if (Directory.Exists(path) && !IsVendorDirectory(path))
            {
                var names = ListDir(path);
                if (names is null)
                {
                    report.Findings.Add(new FolderFinding(path, FolderFindingKind.Unreadable, $"the folder {Path.GetFileName(path)} cannot be read",
                        "check who owns it and what it is set to; nothing in it can be opened until it can be listed"));
                    continue;
                }
                givenFolders.Add(path);
                if (!folders.Contains(path)) folders.Add(path);
                foreach (var name in names)
                {
                    var child = Path.Combine(path, name);
                    if (FileFormats.IsSupported(child) && (File.Exists(child) || IsVendorDirectory(child))) candidates.Add(child);
                }
                continue;
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                report.Findings.Add(new FolderFinding(path, FolderFindingKind.Absent, $"{Path.GetFileName(path)} is not there",
                    "the file moved or was renamed; add it again from where it is now"));
                continue;
            }
            if (!FileFormats.IsSupported(path))
            {
                var extension = FileFormats.GetExtension(path);
                var vendor = OtherFormats.TryGetValue(extension, out var v) ? v : string.Empty;
                var what = vendor.Length > 0 ? $" — {vendor} data" : string.Empty;
                report.Findings.Add(new FolderFinding(path, FolderFindingKind.Unsupported,
                    $"{Path.GetFileName(path)} is a {(extension.Length > 0 ? extension : "nameless")} file{what}, which this build does not read",
                    $"this build reads {string.Join(", ", FileFormats.AllExtensions)}; convert it to mzML with ProteoWizard's msconvert first"));
                continue;
            }
            candidates.Add(path);
            var parent = Path.GetDirectoryName(path);
            if (parent is not null && !folders.Contains(parent)) folders.Add(parent);
        }

        // the files themselves
        foreach (var path in FileFormats.OnePerAcquisition(candidates))
        {
            var key = Key(path);
            if (already.Contains(key))
            {
                report.Findings.Add(new FolderFinding(path, FolderFindingKind.Duplicate, $"{Path.GetFileName(path)} is open already",
                    "it will not be added a second time; every sample in it is in the batch already"));
                continue;
            }
            if (!seen.Add(key))
            {
                report.Findings.Add(new FolderFinding(path, FolderFindingKind.Duplicate, $"{Path.GetFileName(path)} was given twice", "it will be opened once"));
                continue;
            }
            if (!CanRead(path))
            {
                report.Findings.Add(new FolderFinding(path, FolderFindingKind.Unreadable, $"{Path.GetFileName(path)} cannot be read",
                    "check its permissions, or copy it somewhere you own"));
                continue;
            }
            report.Paths.Add(path);
        }

        // the companion every .wiff needs
        var missing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in report.Paths)
        {
            if (!FileFormats.GetExtension(path).Equals(".wiff", StringComparison.Ordinal)) continue;
            var companion = ScanFileOf(path);
            if (!File.Exists(companion))
            {
                var folder = Path.GetDirectoryName(path) ?? string.Empty;
                if (!missing.TryGetValue(folder, out var list)) missing[folder] = list = new List<string>();
                list.Add(path);
            }
            else if (!CanRead(companion))
            {
                report.Findings.Add(new FolderFinding(companion, FolderFindingKind.Unreadable,
                    $"{Path.GetFileName(companion)} is beside {Path.GetFileName(path)} but cannot be read",
                    "the chromatograms will open and the spectra will not; check its permissions"));
            }
        }

        // the folders
        report.Folders.AddRange(folders);
        foreach (var folder in folders)
        {
            var names = ListDir(folder);
            if (names is null) continue;
            var lower = new HashSet<string>(names.Select(n => n.ToLowerInvariant()), StringComparer.Ordinal);
            var wiffs = names.Where(n => n.EndsWith(".wiff", StringComparison.OrdinalIgnoreCase)).Select(n => Path.Combine(folder, n)).ToList();
            var without = wiffs.Where(w => !lower.Contains(Path.GetFileName(w).ToLowerInvariant() + ".scan")).ToList();
            var strays = names.Where(n => n.EndsWith(".scan", StringComparison.OrdinalIgnoreCase) && !lower.Contains(n[..^5].ToLowerInvariant())).ToList();
            var owners = strays.ToDictionary(s => s, s => GuessOwner(Path.Combine(folder, s), without), StringComparer.Ordinal);

            if (missing.TryGetValue(folder, out var missingHere))
            {
                foreach (var path in missingHere)
                {
                    var companion = Path.GetFileName(ScanFileOf(path));
                    var theirs = strays.Where(s => owners[s] == path).ToList();
                    var action = theirs.Count > 0
                        ? $"the folder holds {Some(theirs)}, which belongs to no .wiff here; renamed to {companion} it may be the one — the row for it offers the rename"
                        : $"copy {companion} in from where the acquisition was written; nothing here can stand in for it";
                    report.Findings.Add(new FolderFinding(path, FolderFindingKind.MissingScan,
                        $"{Path.GetFileName(path)} has no {companion} beside it — the chromatograms and the method open, the spectra, the extracted ion chromatograms and the quantitation do not",
                        action));
                }
            }

            if (givenFolders.Contains(folder) || missing.ContainsKey(folder))
            {
                foreach (var stray in strays)
                {
                    var strayPath = Path.Combine(folder, stray);
                    var owner = owners[stray];
                    if (owner.Length > 0)
                    {
                        var wanted = ScanFileOf(owner);
                        report.Findings.Add(new FolderFinding(strayPath, FolderFindingKind.StrayScan,
                            $"{stray} belongs to no .wiff in this folder, and {Path.GetFileName(owner)} is missing its companion",
                            $"a guess from the two names, which is all there is to go on: renamed to {Path.GetFileName(wanted)} it may be the one",
                            strayPath, wanted));
                    }
                    else
                    {
                        report.Findings.Add(new FolderFinding(strayPath, FolderFindingKind.StrayScan,
                            $"{stray} belongs to no .wiff in this folder",
                            "its .wiff is elsewhere, or was renamed; put the pair back together before opening it"));
                    }
                }
            }

            if (!givenFolders.Contains(folder)) continue;
            var paired = names.Where(n => n.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase) && lower.Contains(n[..^1].ToLowerInvariant())).ToList();
            var alone = names.Where(n => n.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase) && !lower.Contains(n[..^1].ToLowerInvariant())).ToList();
            if (paired.Count > 0)
            {
                report.Findings.Add(new FolderFinding(folder, FolderFindingKind.Ignored,
                    $"{paired.Count} .wiff2 file{(paired.Count == 1 ? string.Empty : "s")} ({Some(paired)}): the acquisition's method and file hashes, holding no scan data, read through the .wiff beside each",
                    "nothing is lost — the .wiff of the same name beside each holds the same acquisition, and that is what is opened"));
            }
            if (alone.Count > 0)
            {
                report.Findings.Add(new FolderFinding(folder, FolderFindingKind.Ignored,
                    $"{alone.Count} .wiff2 file{(alone.Count == 1 ? string.Empty : "s")} ({Some(alone)}) with no .wiff of the same name: the method and file hashes, holding no scan data, which the native reader cannot open on its own",
                    "those acquisitions go through msconvert if a converter is set up, and nothing else can stand in — the .wiff2 holds no spectra; fetch their .wiff, or convert them to mzML"));
            }
            var others = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var extension = FileFormats.GetExtension(name);
                if (OtherFormats.ContainsKey(extension))
                {
                    if (!others.TryGetValue(extension, out var group)) others[extension] = group = new List<string>();
                    group.Add(name);
                }
            }
            foreach (var (extension, group) in others)
            {
                report.Findings.Add(new FolderFinding(folder, FolderFindingKind.Ignored,
                    $"{group.Count} {extension} file{(group.Count == 1 ? string.Empty : "s")} ({Some(group)}): {OtherFormats[extension]} data, not read here",
                    "convert them to mzML with ProteoWizard's msconvert and open the mzML"));
            }
        }
        return report;
    }

    /// <summary>
    /// The .wiff a stray .scan most likely belongs to, or empty. Each candidate is compared against the
    /// companion it wants rather than its own name, so the .scan both names end in counts; a guess needs at
    /// least one character of common prefix; ties go to the first name in order.
    /// </summary>
    public static string GuessOwner(string stray, IEnumerable<string> wiffs)
    {
        var best = string.Empty;
        var score = (0, 0);
        foreach (var wiff in wiffs.OrderBy(w => w, StringComparer.Ordinal))
        {
            var wanted = Path.GetFileName(ScanFileOf(wiff));
            var rank = Affinity(Path.GetFileName(stray), wanted);
            if (rank.Item1 >= 1 && rank.CompareTo(score) > 0) { best = wiff; score = rank; }
        }
        return best;
    }

    /// <summary>Carries out the rename a finding proposes; refuses to write over anything. Returns the new path.</summary>
    public static string ApplyRename(FolderFinding finding)
    {
        if (!finding.Renameable) throw new ArgumentException("this finding proposes no rename", nameof(finding));
        if (File.Exists(finding.RenameTo)) throw new IOException($"{Path.GetFileName(finding.RenameTo)} is already there; nothing was renamed");
        File.Move(finding.RenameFrom, finding.RenameTo);
        return finding.RenameTo;
    }

    private static (int, int) Affinity(string a, string b)
    {
        a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix]) prefix++;
        var suffix = 0;
        while (suffix < a.Length && suffix < b.Length && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix]) suffix++;
        var shortest = Math.Min(a.Length, b.Length);
        return prefix + suffix > shortest ? (shortest, 0) : (prefix, suffix);
    }

    private static string Some(IReadOnlyList<string> names, int limit = 3)
    {
        var shown = string.Join(", ", names.Take(limit));
        return names.Count <= limit ? shown : $"{shown} and {names.Count - limit} more";
    }

    private static bool IsVendorDirectory(string path)
    {
        var ext = FileFormats.GetExtension(path);
        return Directory.Exists(path) && (ext == ".raw" || ext == ".d");
    }

    /// <summary>
    /// What a folder holds, without the metadata the operating system keeps in it: names beginning with
    /// a dot are left out, and the one that matters is macOS's <c>._name</c> resource fork on exFAT and SMB
    /// volumes, which ends in .scan and belongs to no .wiff and would otherwise be a finding about a file
    /// the user never made.
    /// </summary>
    private static List<string>? ListDir(string path)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('.')).Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool CanRead(string path)
    {
        try
        {
            if (Directory.Exists(path)) { _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault(); return true; }
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Key(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            var info = new FileInfo(full);
            var target = info.LinkTarget is not null ? info.ResolveLinkTarget(true)?.FullName : null;
            full = target ?? full;
        }
        catch (IOException) { }
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? full.ToLowerInvariant() : full;
    }
}
