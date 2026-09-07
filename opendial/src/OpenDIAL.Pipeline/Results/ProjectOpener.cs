using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialGcMsApi.Parameter;
using CompMs.MsdialIntegrate.Parser;
using OpenDIAL.Pipeline.Model;

namespace OpenDIAL.Pipeline.Results;

/// <summary>A previously processed dataset, re-opened without re-running the pipeline.</summary>
public sealed class OpenedProject
{
    public OpenedProject(string folder, IReadOnlyList<AnalysisFileBean> analysisFiles, AlignmentFileBean? alignmentFile, ParameterBase? parameter,
        DataBaseMapper? dataBaseMapper, IReadOnlyList<ExportedFile> exportedFiles, string? projectFilePath, IonizationMode mode, string source)
    {
        Folder = folder;
        AnalysisFiles = analysisFiles;
        AlignmentFile = alignmentFile;
        Parameter = parameter;
        DataBaseMapper = dataBaseMapper;
        ExportedFiles = exportedFiles;
        ProjectFilePath = projectFilePath;
        Mode = mode;
        Source = source;
    }

    public string Folder { get; }
    public IReadOnlyList<AnalysisFileBean> AnalysisFiles { get; }
    public AlignmentFileBean? AlignmentFile { get; }
    public ParameterBase? Parameter { get; }
    public DataBaseMapper? DataBaseMapper { get; }
    public IReadOnlyList<ExportedFile> ExportedFiles { get; }
    public string? ProjectFilePath { get; }
    public IonizationMode Mode { get; }
    /// <summary>"mdproject" when loaded through upstream ProjectDataStorage, "folder" when reconstructed from .pai2/.dcl files.</summary>
    public string Source { get; }
}

/// <summary>Opens an output folder or .mdproject produced by <see cref="PipelineRunner"/> (or the MS-DIAL console/GUI).</summary>
public static class ProjectOpener
{
    public static async Task<OpenedProject> OpenAsync(string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (File.Exists(path) && path.EndsWith(".mdproject", StringComparison.OrdinalIgnoreCase))
        {
            return await OpenProjectFileAsync(path, ct).ConfigureAwait(false);
        }
        if (Directory.Exists(path))
        {
            var project = Directory.EnumerateFiles(path, "*.mdproject").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (project is not null)
            {
                try
                {
                    return await OpenProjectFileAsync(project, ct).ConfigureAwait(false);
                }
                catch (Exception) when (Directory.EnumerateFiles(path, "*.pai2").Any())
                {
                    // fall back to scanning the folder
                }
            }
            return OpenFolder(path);
        }
        throw new FileNotFoundException("Expected an output folder or a .mdproject file.", path);
    }

    public static async Task<OpenedProject> OpenProjectFileAsync(string projectPath, CancellationToken ct = default)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? string.Empty;
        ProjectDataStorage storage;
        using (var fs = File.Open(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var streamManager = ZipStreamManager.OpenGet(fs))
        {
            ct.ThrowIfCancellationRequested();
            storage = await ProjectDataStorage.LoadAsync(
                streamManager,
                new MsdialIntegrateSerializer(),
                dir => new DirectoryTreeStreamManager(dir),
                projectDir,
                _ => Task.FromResult<string?>(null),
                _ => { }).ConfigureAwait(false);
        }
        if (storage is null || storage.Storages.Count == 0)
        {
            throw new InvalidDataException($"No dataset could be loaded from {projectPath}.");
        }
        storage.FixProjectFolder(projectDir);
        var data = storage.Storages[storage.Storages.Count - 1];
        var mode = data.Parameter is MsdialGcmsParameter ? IonizationMode.GCMS : IonizationMode.LCMS;
        var alignment = data.AlignmentFiles.LastOrDefault(a => File.Exists(a.FilePath) || File.Exists(a.FilePath + "2"));
        return new OpenedProject(projectDir, data.AnalysisFiles, alignment, data.Parameter, data.DataBaseMapper, CollectExports(projectDir), projectPath, mode, "mdproject");
    }

    /// <summary>Reconstructs file beans from the .pai2/.dcl/.arf2 files in a folder (no parameters, no library mapper).</summary>
    public static OpenedProject OpenFolder(string folder)
    {
        folder = Path.GetFullPath(folder);
        var files = new List<AnalysisFileBean>();
        var id = 0;
        foreach (var pai in Directory.EnumerateFiles(folder, "*.pai2").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(pai);          // Name_stamp
            var dcl = Path.Combine(folder, stem + ".dcl");
            var cut = stem.LastIndexOf('_');
            var name = cut > 0 ? stem.Substring(0, cut) : stem;
            var raw = Directory.EnumerateFiles(folder).FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase) && Vendor.FileFormats.IsSupported(f));
            files.Add(new AnalysisFileBean
            {
                AnalysisFileId = id,
                AnalysisFileName = name,
                AnalysisFilePath = raw ?? string.Empty,
                AnalysisFileClass = id.ToString(),
                AnalysisFileIncluded = true,
                AnalysisFileAnalyticalOrder = id + 1,
                PeakAreaBeanInformationFilePath = pai.Substring(0, pai.Length - 1), // .pai2 -> .pai (upstream appends the "2")
                DeconvolutionFilePath = dcl,
            });
            id++;
        }
        if (files.Count == 0)
        {
            throw new FileNotFoundException("No processed files (*.pai2) were found in the folder.", folder);
        }

        AlignmentFileBean? alignment = null;
        var arf = Directory.EnumerateFiles(folder, "AlignResult-*.arf2")
            .Where(f => !Path.GetFileName(f).Contains("_PeakProperties") && !Path.GetFileName(f).Contains("_DriftSopts"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (arf is not null)
        {
            var stem = Path.GetFileNameWithoutExtension(arf);
            alignment = new AlignmentFileBean
            {
                FileID = 0,
                FileName = stem,
                FilePath = arf.Substring(0, arf.Length - 1),
                SpectraFilePath = Path.Combine(folder, stem + ".dcl"),
                EicFilePath = Path.Combine(folder, stem + ".EIC.aef"),
            };
        }
        return new OpenedProject(folder, files, alignment, null, null, CollectExports(folder), null, IonizationMode.LCMS, "folder");
    }

    public static IReadOnlyList<ExportedFile> CollectExports(string folder)
    {
        var list = new List<ExportedFile>();
        foreach (var f in Directory.EnumerateFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(f);
            var label = Path.GetExtension(f).ToLowerInvariant() switch
            {
                ".mdpeak" => "Peak table",
                ".mdscan" => "Scan table",
                ".mdmsp" => "MS/MS spectra",
                ".mdalign" => "Alignment table",
                ".mztab" => "mzTab-M",
                ".mdproject" => "Project",
                ".tsv" when name.EndsWith(".qa.tsv", StringComparison.OrdinalIgnoreCase) => "QA matrix",
                ".txt" when name == "opendial_method.txt" => "Method file",
                _ => null,
            };
            if (label is not null)
            {
                list.Add(new ExportedFile($"{label} ({name})", f));
            }
        }
        return list;
    }
}
