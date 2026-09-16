using System.Text.Json;
using System.Text.Json.Serialization;
using MilX.Pipeline.Model;

namespace MilX.Pipeline.Project;

/// <summary>One sample of an MIL-X project file (the batch table row).</summary>
public sealed class ProjectSample
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = "1";
    public SampleType SampleType { get; set; } = SampleType.Sample;
    public AcquisitionMode Acquisition { get; set; } = AcquisitionMode.DDA;
    /// <summary>The polarity this injection was acquired in; a batch with both is run twice.</summary>
    public IonPolarity Polarity { get; set; } = IonPolarity.Positive;
    public int AnalyticalOrder { get; set; } = 1;
    public int Batch { get; set; } = 1;
    /// <summary>The second factor of a two-factor design (time point, diet, genotype); empty when there is none.</summary>
    public string Factor { get; set; } = string.Empty;
    public double Dilution { get; set; } = 1;
    public string Comment { get; set; } = string.Empty;
    public bool Included { get; set; } = true;
    /// <summary>0-based sample inside a multi-sample .wiff batch (0 for every other file).</summary>
    public int SampleIndex { get; set; }
}

/// <summary>
/// The MIL-X project: the batch, the method text, the output folder and (once processed) the
/// path of the MS-DIAL <c>.mdproject</c>. Saved as JSON with the <see cref="Extension"/> extension,
/// alongside the MS-DIAL files, so a run is reproducible from the file.
///
/// Before 1.0 the program was OpenDIAL and the same JSON was written as <c>.odproj</c>. Those
/// files still open (<see cref="IsProjectFile"/> accepts both names); a save writes <c>.milx</c>.
/// </summary>
public sealed class MilXProject
{
    public const string Extension = ".milx";

    /// <summary>The extension the project file had before 1.0. Read, never written.</summary>
    public const string LegacyExtension = ".odproj";

    /// <summary>Whether a path names a project file, under either of its names.</summary>
    public static bool IsProjectFile(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>.milx</c> path a project under the old name is saved to: the same file, renamed.</summary>
    public static string ModernPath(string path) =>
        path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase) ? path[..^LegacyExtension.Length] + Extension : path;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public IonizationMode Mode { get; set; } = IonizationMode.LCMS;
    public string OutputFolder { get; set; } = string.Empty;
    public string? MdprojectPath { get; set; }
    public string MethodText { get; set; } = string.Empty;
    public List<ProjectSample> Samples { get; set; } = new();

    /// <summary>Path the project was loaded from / saved to (not serialised).</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    public static async Task<MilXProject> LoadAsync(string path, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var project = JsonSerializer.Deserialize<MilXProject>(json, JsonOptions) ?? throw new InvalidDataException($"{path} is not an MIL-X project.");
        project.FilePath = System.IO.Path.GetFullPath(path);
        var folder = System.IO.Path.GetDirectoryName(project.FilePath) ?? string.Empty;
        project.OutputFolder = Absolute(folder, project.OutputFolder);
        project.MdprojectPath = string.IsNullOrEmpty(project.MdprojectPath) ? null : Absolute(folder, project.MdprojectPath);
        foreach (var sample in project.Samples)
        {
            sample.Path = Absolute(folder, sample.Path);
        }
        return project;
    }

    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        path = System.IO.Path.GetFullPath(path);
        var folder = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        Directory.CreateDirectory(folder);
        var copy = new MilXProject
        {
            Version = Version,
            Name = Name,
            Mode = Mode,
            OutputFolder = Relative(folder, OutputFolder),
            MdprojectPath = MdprojectPath is null ? null : Relative(folder, MdprojectPath),
            MethodText = MethodText,
            Samples = Samples.Select(s => new ProjectSample
            {
                Path = Relative(folder, s.Path),
                Name = s.Name,
                Class = s.Class,
                SampleType = s.SampleType,
                Acquisition = s.Acquisition,
                Polarity = s.Polarity,
                AnalyticalOrder = s.AnalyticalOrder,
                Batch = s.Batch,
                Factor = s.Factor,
                Dilution = s.Dilution,
                Comment = s.Comment,
                Included = s.Included,
                SampleIndex = s.SampleIndex,
            }).ToList(),
        };
        var json = JsonSerializer.Serialize(copy, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        FilePath = path;
    }

    /// <summary>The newest project file in a folder, under either name, or null.</summary>
    public static string? FindInFolder(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder, "*" + Extension)
            .Concat(Directory.EnumerateFiles(folder, "*" + LegacyExtension))
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static string Absolute(string folder, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, path));
    }

    private static string Relative(string folder, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            var rel = System.IO.Path.GetRelativePath(folder, path);
            // keep it relative only when it stays inside a nearby tree; deep "../../.." chains are not more portable
            return rel.StartsWith("../../..", StringComparison.Ordinal) || rel.StartsWith(@"..\..\..", StringComparison.Ordinal) ? path : rel;
        }
        catch
        {
            return path;
        }
    }
}
