using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;

namespace MilX.Pipeline.Model;

public sealed record ExportedFile(string Label, string Path);

/// <summary>Outcome of a pipeline run: the upstream file beans needed to load results plus the list of exports.</summary>
public sealed class PipelineResult
{
    public PipelineResult(string outputFolder, IReadOnlyList<AnalysisFileBean> analysisFiles, AlignmentFileBean? alignmentFile,
        ParameterBase parameter, DataBaseMapper? dataBaseMapper, IReadOnlyList<ExportedFile> exportedFiles, string? projectFilePath, IonizationMode mode)
    {
        OutputFolder = outputFolder;
        AnalysisFiles = analysisFiles;
        AlignmentFile = alignmentFile;
        Parameter = parameter;
        DataBaseMapper = dataBaseMapper;
        ExportedFiles = exportedFiles;
        ProjectFilePath = projectFilePath;
        Mode = mode;
    }

    public string OutputFolder { get; }
    public IReadOnlyList<AnalysisFileBean> AnalysisFiles { get; }
    public AlignmentFileBean? AlignmentFile { get; }
    public ParameterBase Parameter { get; }
    /// <summary>Maps annotation results back to library references (used for mirror plots). Null when no library was loaded.</summary>
    public DataBaseMapper? DataBaseMapper { get; }
    public IReadOnlyList<ExportedFile> ExportedFiles { get; }
    public string? ProjectFilePath { get; }
    public IonizationMode Mode { get; }
    public TimeSpan Elapsed { get; init; }
}
