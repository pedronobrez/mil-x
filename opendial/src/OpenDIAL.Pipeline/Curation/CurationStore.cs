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

    public static string TagFileFor(string alignmentFilePath) =>
        Path.Combine(Path.GetDirectoryName(alignmentFilePath) ?? string.Empty,
                     Path.GetFileNameWithoutExtension(alignmentFilePath) + "_tags.xml");

    public static string SidecarFileFor(string alignmentFilePath) =>
        Path.Combine(Path.GetDirectoryName(alignmentFilePath) ?? string.Empty,
                     Path.GetFileNameWithoutExtension(alignmentFilePath) + "_curation.json");

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

    public bool HasAnything(int spotId) => _bySpot.TryGetValue(spotId, out var c) && !c.IsEmpty;

    public IReadOnlyCollection<PeakSpotTagKind> TagsOf(int spotId) =>
        _bySpot.TryGetValue(spotId, out var c) ? c.Tags : Array.Empty<PeakSpotTagKind>();

    public bool HasTag(int spotId, PeakSpotTagKind tag) => _bySpot.TryGetValue(spotId, out var c) && c.Tags.Contains(tag);

    public void SetTag(int spotId, PeakSpotTagKind tag, bool on) {
        var c = Get(spotId);
        var changed = on ? c.Tags.Add(tag) : c.Tags.Remove(tag);
        if (changed) IsDirty = true;
    }

    public void ClearTags(int spotId) {
        var c = Get(spotId);
        if (c.Tags.Count == 0) return;
        c.Tags.Clear();
        IsDirty = true;
    }

    public void SetComment(int spotId, string comment) {
        var c = Get(spotId);
        comment ??= string.Empty;
        if (c.Comment == comment) return;
        c.Comment = comment;
        IsDirty = true;
    }

    public void SetManualName(int spotId, string name) {
        var c = Get(spotId);
        name ??= string.Empty;
        if (c.ManualName == name) return;
        c.ManualName = name;
        IsDirty = true;
    }

    public void SetReviewed(int spotId, bool reviewed) {
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
            if (payload?.Spots is null) return;
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
        if (spots.Count == 0) {
            if (File.Exists(SidecarPath)) File.Delete(SidecarPath);
            return;
        }
        using var stream = File.Create(SidecarPath);
        JsonSerializer.Serialize(stream, new Sidecar { Version = 1, Spots = spots },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    private sealed class Sidecar
    {
        public int Version { get; set; }
        public List<SidecarSpot>? Spots { get; set; }
    }

    private sealed class SidecarSpot
    {
        public int Id { get; set; }
        public string? Comment { get; set; }
        public string? ManualName { get; set; }
        public bool Reviewed { get; set; }
    }
}
