using System.Text.Json;
using System.Text.Json.Serialization;
using OpenDIAL.Pipeline.Model;

namespace OpenDIAL.Pipeline.Project;

/// <summary>One sample of an OpenDIAL project file (the batch table row).</summary>
public sealed class ProjectSample
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = "1";
    public SampleType SampleType { get; set; } = SampleType.Sample;
    public AcquisitionMode Acquisition { get; set; } = AcquisitionMode.DDA;
    public int AnalyticalOrder { get; set; } = 1;
    public int Batch { get; set; } = 1;
    public double Dilution { get; set; } = 1;
    public string Comment { get; set; } = string.Empty;
    public bool Included { get; set; } = true;
}

/// <summary>
/// The OpenDIAL project: the batch, the method text, the output folder and (once processed) the
/// path of the MS-DIAL <c>.mdproject</c>. Saved as JSON with the <see cref="Extension"/> extension,
/// alongside the MS-DIAL files, so a run is reproducible from the file.
/// </summary>
public sealed class OpenDialProject
{
    public const string Extension = ".odproj";

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

    public static async Task<OpenDialProject> LoadAsync(string path, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var project = JsonSerializer.Deserialize<OpenDialProject>(json, JsonOptions) ?? throw new InvalidDataException($"{path} is not an OpenDIAL project.");
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
        var copy = new OpenDialProject
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
                AnalyticalOrder = s.AnalyticalOrder,
                Batch = s.Batch,
                Dilution = s.Dilution,
                Comment = s.Comment,
                Included = s.Included,
            }).ToList(),
        };
        var json = JsonSerializer.Serialize(copy, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        FilePath = path;
    }

    /// <summary>The newest project file in a folder, or null.</summary>
    public static string? FindInFolder(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder, "*" + Extension).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
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
