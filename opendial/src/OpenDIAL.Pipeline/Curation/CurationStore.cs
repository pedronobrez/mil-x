using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace OpenDIAL.Pipeline.Curation;

/// <summary>What a reviewer decided about one aligned feature.</summary>
public sealed class SpotCuration
{
    public HashSet<PeakSpotTagKind> Tags { get; init; } = new();
    public string Comment { get; set; } = string.Empty;
    /// <summary>Name chosen by hand from the candidate list, replacing the automatic annotation.</summary>
    public string ManualName { get; set; } = string.Empty;
    public bool Reviewed { get; set; }

    public bool IsEmpty => Tags.Count == 0 && Comment.Length == 0 && ManualName.Length == 0 && !Reviewed;

    /// <summary>A copy, for the undo stack to hold on to.</summary>
    public SpotCuration Copy() => new() { Tags = new HashSet<PeakSpotTagKind>(Tags), Comment = Comment, ManualName = ManualName, Reviewed = Reviewed };
}

/// <summary>
/// Reads and writes the review state of an alignment result.
///
/// Tags go to "&lt;alignment&gt;_tags.xml" in MS-DIAL's own format, so a curation done here shows up
/// in the Windows application and one done there shows up here. Everything MS-DIAL keeps inside the
/// binary alignment file instead — the comment, a hand-picked annotation, whether the feature has
/// been looked at — goes to a sibling "&lt;alignment&gt;_curation.json", which leaves the binary
/// files untouched.
/// </summary>
public sealed class CurationStore
{
    private readonly Dictionary<int, SpotCuration> _bySpot = new();

    private CurationStore(string tagPath, string sidecarPath) {
        TagFilePath = tagPath;
        SidecarPath = sidecarPath;
    }

    public string TagFilePath { get; }
    public string SidecarPath { get; }
    public bool IsDirty { get; private set; }

    /// <summary>The internal standard chosen for each lipid class, by feature id; kept with the review because it refers to this alignment's ids.</summary>
    public Dictionary<string, int> InternalStandards { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void SetInternalStandards(IEnumerable<KeyValuePair<string, int?>> assignments) {
        var before = InternalStandards.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}").ToList();
        InternalStandards.Clear();
        foreach (var (cls, id) in assignments) {
            if (id is not null) InternalStandards[cls] = id.Value;
        }
        var after = InternalStandards.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}").ToList();
        if (!before.SequenceEqual(after)) IsDirty = true;
    }

    public static string TagFileFor(string alignmentFilePath) =>
        Path.Combine(Path.GetDirectoryName(alignmentFilePath) ?? string.Empty,
                     Path.GetFileNameWithoutExtension(alignmentFilePath) + "_tags.xml");

    public static string SidecarFileFor(string alignmentFilePath) =>
        Path.Combine(Path.GetDirectoryName(alignmentFilePath) ?? string.Empty,
                     Path.GetFileNameWithoutExtension(alignmentFilePath) + "_curation.json");

    /// <summary>
    /// A review that belongs to no file: a merged view of two alignments has ids that exist in
    /// neither of them, so its tags have nowhere to be written and must not be. Saving one is a
    /// no-op rather than an error, so the callers that save on the way out need no special case.
    /// </summary>
    public static CurationStore InMemory() => new(string.Empty, string.Empty);

    /// <summary>True when this review is a merged view rather than one alignment's own.</summary>
    public bool IsInMemory => TagFilePath.Length == 0;

    public static CurationStore Load(string alignmentFilePath) {
        var store = new CurationStore(TagFileFor(alignmentFilePath), SidecarFileFor(alignmentFilePath));
        store.ReadTags();
        store.ReadSidecar();
        store.IsDirty = false;
        return store;
    }

    public SpotCuration Get(int spotId) {
        if (!_bySpot.TryGetValue(spotId, out var c)) _bySpot[spotId] = c = new SpotCuration();
        return c;
    }

    // ---- undo ---------------------------------------------------------------------------------
    // Confirm all shown over a filter of two thousand features is a decision a reviewer should be
    // able to take back. Every change records the state of the spots it touched before touching
    // them, and a bulk command wraps its changes in one step so that one undo puts all of it back.

    private sealed record Step(string Label, List<(int Spot, SpotCuration Before)> Before);

    private readonly Stack<Step> _undo = new();
    private readonly Stack<Step> _redo = new();
    private List<(int Spot, SpotCuration Before)>? _open;
    private string _openLabel = string.Empty;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoLabel => _undo.Count > 0 ? _undo.Peek().Label : string.Empty;
    public string RedoLabel => _redo.Count > 0 ? _redo.Peek().Label : string.Empty;

    /// <summary>Opens one undo step; every change until it is disposed goes back together.</summary>
    public IDisposable Begin(string label) {
        if (_open is not null) return new Nothing();      // already inside one; the outer step wins
        _open = new List<(int, SpotCuration)>();
        _openLabel = label;
        return new Transaction(this);
    }

    private void Record(int spotId, string label) {
        if (_open is not null) {
            if (_open.Any(e => e.Spot == spotId)) return;
            _open.Add((spotId, Get(spotId).Copy()));
            return;
        }
        _undo.Push(new Step(label, new List<(int, SpotCuration)> { (spotId, Get(spotId).Copy()) }));
        _redo.Clear();
        Trim();
    }

    private void Close() {
        var open = _open;
        _open = null;
        if (open is null || open.Count == 0) return;
        _undo.Push(new Step(_openLabel, open));
        _redo.Clear();
        Trim();
    }

    private void Trim() {
        if (_undo.Count <= 200) return;
        var kept = _undo.ToArray().Take(200).Reverse().ToList();
        _undo.Clear();
        foreach (var step in kept) _undo.Push(step);
    }

    /// <summary>Puts the last step back; answers what it was, or empty when there was nothing.</summary>
    public string Undo() => Move(_undo, _redo);

    public string Redo() => Move(_redo, _undo);

    private string Move(Stack<Step> from, Stack<Step> to) {
        if (from.Count == 0) return string.Empty;
        var step = from.Pop();
        var mirror = new List<(int Spot, SpotCuration Before)>();
        foreach (var (spot, before) in step.Before) {
            mirror.Add((spot, Get(spot).Copy()));
            _bySpot[spot] = before.Copy();
        }
        to.Push(new Step(step.Label, mirror));
        IsDirty = true;
        return step.Label;
    }

    private sealed class Transaction : IDisposable {
        private readonly CurationStore _store;
        private bool _done;
        public Transaction(CurationStore store) => _store = store;
        public void Dispose() { if (_done) return; _done = true; _store.Close(); }
    }

    private sealed class Nothing : IDisposable { public void Dispose() { } }

    public bool HasAnything(int spotId) => _bySpot.TryGetValue(spotId, out var c) && !c.IsEmpty;

    public IReadOnlyCollection<PeakSpotTagKind> TagsOf(int spotId) =>
        _bySpot.TryGetValue(spotId, out var c) ? c.Tags : Array.Empty<PeakSpotTagKind>();

    public bool HasTag(int spotId, PeakSpotTagKind tag) => _bySpot.TryGetValue(spotId, out var c) && c.Tags.Contains(tag);

    public void SetTag(int spotId, PeakSpotTagKind tag, bool on) {
        Record(spotId, "tag");
        var c = Get(spotId);
        var changed = on ? c.Tags.Add(tag) : c.Tags.Remove(tag);
        if (changed) IsDirty = true;
    }

    public void ClearTags(int spotId) {
        Record(spotId, "clear the tags");
        var c = Get(spotId);
        if (c.Tags.Count == 0) return;
        c.Tags.Clear();
        IsDirty = true;
    }

    public void SetComment(int spotId, string comment) {
        Record(spotId, "comment");
        var c = Get(spotId);
        comment ??= string.Empty;
        if (c.Comment == comment) return;
        c.Comment = comment;
        IsDirty = true;
    }

    public void SetManualName(int spotId, string name) {
        Record(spotId, "name");
        var c = Get(spotId);
        name ??= string.Empty;
        if (c.ManualName == name) return;
        c.ManualName = name;
        IsDirty = true;
    }

    public void SetReviewed(int spotId, bool reviewed) {
        Record(spotId, "reviewed");
        var c = Get(spotId);
        if (c.Reviewed == reviewed) return;
        c.Reviewed = reviewed;
        IsDirty = true;
    }

    public int TaggedCount(PeakSpotTagKind tag) => _bySpot.Values.Count(c => c.Tags.Contains(tag));
    public int ReviewedCount => _bySpot.Values.Count(c => c.Reviewed || c.Tags.Count > 0);

    // ------------------------------------------------------------------ reading

    private void ReadTags() {
        if (!File.Exists(TagFilePath)) return;
        try {
            var doc = XElement.Load(TagFilePath);
            foreach (var peak in doc.Descendants("Peak")) {
                if (!int.TryParse(peak.Attribute("Id")?.Value, out var id)) continue;
                foreach (var tag in peak.Elements("Tag")) {
                    if (int.TryParse(tag.Value, out var v) && Enum.IsDefined(typeof(PeakSpotTagKind), v)) {
                        Get(id).Tags.Add((PeakSpotTagKind)v);
                    }
                }
            }
        }
        catch (Exception) {
            // an unreadable tag file must not stop the results from opening
        }
    }

    private void ReadSidecar() {
        if (!File.Exists(SidecarPath)) return;
        try {
            using var stream = File.OpenRead(SidecarPath);
            var payload = JsonSerializer.Deserialize<Sidecar>(stream);
            if (payload is null) return;
            if (payload.InternalStandards is not null) {
                foreach (var (cls, id) in payload.InternalStandards) InternalStandards[cls] = id;
            }
            if (payload.Spots is null) return;
            foreach (var entry in payload.Spots) {
                var c = Get(entry.Id);
                c.Comment = entry.Comment ?? string.Empty;
                c.ManualName = entry.ManualName ?? string.Empty;
                c.Reviewed = entry.Reviewed;
            }
        }
        catch (Exception) {
            // same: the sidecar is a convenience, never a requirement
        }
    }

    // ------------------------------------------------------------------ writing

    /// <summary>Writes both files. The tag file keeps MS-DIAL's schema exactly.</summary>
    public void Save() {
        if (IsInMemory) { IsDirty = false; return; }
        WriteTags();
        WriteSidecar();
        IsDirty = false;
    }

    private void WriteTags() {
        var definitions = new XElement("Definitions");
        foreach (var kind in PeakSpotTagKindExtensions.All) {
            definitions.Add(new XElement("Tag", new XElement("Id", (int)kind), new XElement("Label", kind.Label())));
        }
        var peaks = new XElement("Peaks");
        foreach (var (id, curation) in _bySpot.OrderBy(kv => kv.Key)) {
            if (curation.Tags.Count == 0) continue;
            var peak = new XElement("Peak");
            peak.SetAttributeValue("Id", id);
            foreach (var tag in curation.Tags.OrderBy(t => (int)t)) peak.Add(new XElement("Tag", (int)tag));
            peaks.Add(peak);
        }
        new XElement("PeakSpotTags", definitions, peaks).Save(TagFilePath);
    }

    private void WriteSidecar() {
        var spots = _bySpot
            .Where(kv => kv.Value.Comment.Length > 0 || kv.Value.ManualName.Length > 0 || kv.Value.Reviewed)
            .OrderBy(kv => kv.Key)
            .Select(kv => new SidecarSpot {
                Id = kv.Key,
                Comment = kv.Value.Comment.Length == 0 ? null : kv.Value.Comment,
                ManualName = kv.Value.ManualName.Length == 0 ? null : kv.Value.ManualName,
                Reviewed = kv.Value.Reviewed,
            })
            .ToList();
        if (spots.Count == 0 && InternalStandards.Count == 0) {
            if (File.Exists(SidecarPath)) File.Delete(SidecarPath);
            return;
        }
        using var stream = File.Create(SidecarPath);
        JsonSerializer.Serialize(stream, new Sidecar { Version = 1, Spots = spots, InternalStandards = InternalStandards.Count == 0 ? null : new Dictionary<string, int>(InternalStandards) },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    private sealed class Sidecar
    {
        public int Version { get; set; }
        public List<SidecarSpot>? Spots { get; set; }
        public Dictionary<string, int>? InternalStandards { get; set; }
    }

    private sealed class SidecarSpot
    {
        public int Id { get; set; }
        public string? Comment { get; set; }
        public string? ManualName { get; set; }
        public bool Reviewed { get; set; }
    }
}
