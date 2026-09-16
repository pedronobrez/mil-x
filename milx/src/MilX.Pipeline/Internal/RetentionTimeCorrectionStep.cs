// Adapted from MS-DIAL 5 (LGPL-3.0):
//   tests/MSDIAL5/MsdialCoreTestApp/Process/RetentionTimeCorrectionProcess.cs
// Original copyright (c) RIKEN and the MS-DIAL contributors.
// Changes for MIL-X: logging via callback, cancellation support, the optional
// "peak selection file" replay is not ported (selections are always automatic).

using System.Globalization;
using System.Text;
using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Parser;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;

namespace MilX.Pipeline.Internal;

internal static class RetentionTimeCorrectionStep
{
    private const string SelectionHeader = "File path\tFile name\tStandard ID\tStandard name\tReference RT (min)\tDetected RT (min)\tSelected RT (min)\tUse\tPeak height";

    public static void Prepare(IReadOnlyList<AnalysisFileBean> analysisFiles, ParameterBase parameter, string outputFolder, Action<string> log, CancellationToken ct)
    {
        var rtParameter = parameter.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam;
        if (!rtParameter.ExcuteRtCorrection)
        {
            return;
        }

        var standards = LoadStandards(parameter);
        parameter.RetentionTimeCorrectionCommon.StandardLibrary = standards;
        log($"RT correction started with {standards.Count} anchor peak(s).");

        var originalMinimumAmplitude = parameter.MinimumAmplitude;
        parameter.MinimumAmplitude = standards.Min(standard => standard.MinimumPeakHeight);
        try
        {
            var completed = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, parameter.NumThreads),
                CancellationToken = ct,
            };
            Parallel.ForEach(analysisFiles, parallelOptions, file =>
            {
                var providerFactory = new StandardDataProviderFactory { IgnoreRtCorrection = true };
                var provider = providerFactory.Create(file);
                RetentionTimeCorrection.Execute(file, parameter, provider);
                var current = Interlocked.Increment(ref completed);
                log($"RT correction anchor detection: {current}/{analysisFiles.Count} ({file.AnalysisFileName})");
            });
        }
        finally
        {
            parameter.MinimumAmplitude = originalMinimumAmplitude;
        }

        Directory.CreateDirectory(outputFolder);
        parameter.RetentionTimeCorrectionCommon.SampleCellInfoListList = analysisFiles
            .Select(file => file.RetentionTimeCorrectionBean.StandardList
                .Select(pair => pair.SamplePeakAreaBean.PeakFeature.ChromXsTop.RT.Value <= 0d ? SampleListCellInfo.Zero : SampleListCellInfo.Normal)
                .ToList())
            .ToList();

        var commonStandards = RetentionTimeCorrectionMethod.MakeCommonStdList(analysisFiles.ToList(), standards);
        RetentionTimeCorrectionMethod.UpdateRtCorrectionBean(
            analysisFiles.ToList(),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parameter.NumThreads) },
            rtParameter,
            commonStandards);

        var auditPath = Path.Combine(outputFolder, "rt_correction_peak_selections.tsv");
        WriteSelectionFile(auditPath, analysisFiles);
        log($"RT correction peak selections: {auditPath}");
        log("RT correction finished.");
    }

    private static void WriteSelectionFile(string outputPath, IReadOnlyList<AnalysisFileBean> analysisFiles)
    {
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
        writer.WriteLine(SelectionHeader);
        foreach (var file in analysisFiles)
        {
            foreach (var standard in file.RetentionTimeCorrectionBean.StandardList.OrderBy(item => item.Reference.ScanID))
            {
                var selectedRt = standard.SamplePeakAreaBean.PeakFeature.ChromXsTop.RT.Value;
                writer.WriteLine(string.Join("\t",
                    file.AnalysisFilePath,
                    file.AnalysisFileName,
                    standard.Reference.ScanID.ToString(CultureInfo.InvariantCulture),
                    standard.Reference.Name,
                    standard.Reference.ChromXs.RT.Value.ToString(CultureInfo.InvariantCulture),
                    selectedRt.ToString(CultureInfo.InvariantCulture),
                    selectedRt.ToString(CultureInfo.InvariantCulture),
                    (selectedRt > 0d).ToString(),
                    standard.SamplePeakAreaBean.PeakFeature.PeakHeightTop.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private static List<MoleculeMsReference> LoadStandards(ParameterBase parameter)
    {
        if (parameter.CompoundListForRtCorrectionPath.IsEmptyOrNull())
        {
            throw new InvalidOperationException("Compounds library file path for RT correction is required when Execute RT correction is True.");
        }
        if (!File.Exists(parameter.CompoundListForRtCorrectionPath))
        {
            throw new FileNotFoundException("RT correction anchor library was not found.", parameter.CompoundListForRtCorrectionPath);
        }

        var standards = TextLibraryParser.StandardTextLibraryReader(parameter.CompoundListForRtCorrectionPath, out var error)
            ?.Where(standard => standard.IsTargetMolecule)
            .OrderBy(standard => standard.ChromXs.RT.Value)
            .ToList() ?? new List<MoleculeMsReference>();
        if (!error.IsEmptyOrNull())
        {
            throw new InvalidDataException(error);
        }
        if (standards.Count == 0)
        {
            throw new InvalidDataException("The RT correction anchor library contains no enabled standards.");
        }
        return standards;
    }
}
