// Adapted from MS-DIAL 5 (LGPL-3.0):
//   tests/MSDIAL5/MsdialCoreTestApp/Parser/AnalysisFilesParser.cs
//   tests/MSDIAL5/MsdialCoreTestApp/Parser/AlignmentResultParser.cs
// Original copyright (c) RIKEN and the MS-DIAL contributors.
// Changes for MIL-X: intermediate files are written to the output folder instead of next to
// the raw data, per-file metadata comes from InputFile, and there is no interactive prompt.

using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using MilX.Pipeline.Model;

namespace MilX.Pipeline.Internal;

internal static class AnalysisFileFactory
{
    public static string MakeRunStamp(DateTime dt) => dt.ToString("yyyyMMddHHmmss");

    public static AnalysisFileType ToUpstream(SampleType type) => type switch
    {
        SampleType.Blank => AnalysisFileType.Blank,
        SampleType.QC => AnalysisFileType.QC,
        SampleType.Standard => AnalysisFileType.Standard,
        _ => AnalysisFileType.Sample,
    };

    public static SampleType FromUpstream(AnalysisFileType type) => type switch
    {
        AnalysisFileType.Blank => SampleType.Blank,
        AnalysisFileType.QC => SampleType.QC,
        AnalysisFileType.Standard => SampleType.Standard,
        _ => SampleType.Sample,
    };

    public static AcquisitionType ToUpstream(AcquisitionMode mode) => mode switch
    {
        AcquisitionMode.SWATH => AcquisitionType.SWATH,
        AcquisitionMode.AIF => AcquisitionType.AIF,
        _ => AcquisitionType.DDA,
    };

    public static List<AnalysisFileBean> Create(IReadOnlyList<InputFile> inputs, IReadOnlyList<string> resolvedPaths, string outputFolder, string stamp)
    {
        var files = new List<AnalysisFileBean>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var path = resolvedPaths[i];
            var name = string.IsNullOrWhiteSpace(input.Name) ? Path.GetFileNameWithoutExtension(path) : input.Name;
            files.Add(new AnalysisFileBean
            {
                AnalysisFileId = i,
                AnalysisFileIncluded = input.Included,
                AnalysisFileName = name,
                AnalysisFilePath = path,
                AnalysisFileAnalyticalOrder = input.AnalyticalOrder > 0 ? input.AnalyticalOrder : i + 1,
                AnalysisFileClass = string.IsNullOrWhiteSpace(input.Class) ? i.ToString() : input.Class,
                AnalysisFileType = ToUpstream(input.SampleType),
                AcquisitionType = ToUpstream(input.Acquisition),
                AnalysisBatch = input.Batch > 0 ? input.Batch : 1,
                DeconvolutionFilePath = Path.Combine(outputFolder, $"{name}_{stamp}.dcl"),
                PeakAreaBeanInformationFilePath = Path.Combine(outputFolder, $"{name}_{stamp}.pai"),
                RetentionTimeCorrectionBean = new RetentionTimeCorrectionBean(Path.Combine(outputFolder, $"{name}_{stamp}.rtc")),
            });
        }
        return files;
    }

    public static AlignmentFileBean CreateAlignmentFile(string outputFolder, string stamp)
    {
        var alignFileString = "AlignResult-" + stamp;
        return new AlignmentFileBean
        {
            FileID = 0,
            FileName = alignFileString,
            FilePath = Path.Combine(outputFolder, alignFileString + ".arf"),
            SpectraFilePath = Path.Combine(outputFolder, alignFileString + ".dcl"),
            EicFilePath = Path.Combine(outputFolder, alignFileString + ".EIC.aef"),
        };
    }
}
