// Adapted from MS-DIAL 5 (LGPL-3.0):
//   tests/MSDIAL5/MsdialCoreTestApp/Process/LcmsProcess.cs
//   tests/MSDIAL5/MsdialCoreTestApp/Process/GcmsProcess.cs
//   tests/MSDIAL5/MsdialCoreTestApp/Process/CommonProcess.cs
// Original copyright (c) RIKEN and the MS-DIAL contributors.
// Changes for MIL-X: request/progress/cancellation based API, all intermediate files are
// written to the output folder, no console interaction, exports are reported back to the caller.

using System.Diagnostics;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Utility;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialGcMsApi.Algorithm;
using CompMs.MsdialGcMsApi.Algorithm.Alignment;
using CompMs.MsdialGcMsApi.DataObj;
using CompMs.MsdialGcMsApi.Export;
using CompMs.MsdialGcMsApi.Parameter;
using CompMs.MsdialGcMsApi.Parser;
using CompMs.MsdialIntegrate.Parser;
using CompMs.MsdialLcmsApi.Parameter;
using CompMs.MsdialLcMsApi.Algorithm.Alignment;
using CompMs.MsdialLcMsApi.Algorithm.Annotation;
using CompMs.MsdialLcMsApi.DataObj;
using CompMs.MsdialLcMsApi.Export;
using MilX.Pipeline.Internal;
using MilX.Pipeline.Model;
using MilX.Pipeline.Parameters;
using MilX.Pipeline.Vendor;
using GcmsFileProcess = CompMs.MsdialGcMsApi.Process.FileProcess;
using LcmsFileProcess = CompMs.MsdialLcMsApi.Process.FileProcess;

namespace MilX.Pipeline;

/// <summary>
/// Runs the complete MS-DIAL workflow (peak picking, deconvolution, annotation, alignment, exports)
/// for a <see cref="PipelineRequest"/>. Safe to call from a background task; progress is reported
/// synchronously on whatever thread the upstream code happens to run on.
/// </summary>
public sealed class PipelineRunner
{
    /// <summary>Upstream MS-DIAL version this port tracks.</summary>
    public const string UpstreamVersion = "5.5.260817";

    /// <summary>Echo upstream console output to the real console in addition to the progress sink.</summary>
    public bool EchoConsole { get; set; } = false;

    public async Task<PipelineResult> RunAsync(PipelineRequest request, IProgress<PipelineProgress>? progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reporter = new Reporter(progress);
        var stopwatch = Stopwatch.StartNew();

        using var capture = ConsoleCapture.Start(line => reporter.Log(line), EchoConsole);
        try
        {
            var result = await RunCoreAsync(request, reporter, ct).ConfigureAwait(false);
            reporter.Stage("Done", null, 100, 100, $"Finished in {stopwatch.Elapsed:mm\\:ss}.");
            return new PipelineResult(result.OutputFolder, result.AnalysisFiles, result.AlignmentFile, result.Parameter, result.DataBaseMapper, result.ExportedFiles, result.ProjectFilePath, result.Mode)
            {
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            reporter.Log("Processing cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            reporter.Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private async Task<PipelineResult> RunCoreAsync(PipelineRequest request, Reporter reporter, CancellationToken ct)
    {
        // ---- validation -------------------------------------------------------------
        var inputs = request.InputFiles.Where(f => f.Included).ToList();
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException("No input files were selected.");
        }
        if (string.IsNullOrWhiteSpace(request.OutputFolder))
        {
            throw new InvalidOperationException("An output folder is required.");
        }
        foreach (var input in inputs)
        {
            if (!File.Exists(input.Path) && !Directory.Exists(input.Path))
            {
                throw new FileNotFoundException($"Input file not found: {input.Path}", input.Path);
            }
        }
        var outputFolder = Path.GetFullPath(request.OutputFolder);
        Directory.CreateDirectory(outputFolder);
        reporter.Stage("Setup", null, 0, 0, $"Output folder: {outputFolder}");

        // ---- vendor conversion --------------------------------------------------------
        var conversion = request.VendorConversion ?? new MsconvertVendorConversionService();
        var resolvedPaths = new List<string>(inputs.Count);
        var logProgress = new DelegateProgress<string>(reporter.Log);
        for (var i = 0; i < inputs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var input = inputs[i];
            if (WiffSupport.CanReadNatively(input.Path))
            {
                reporter.Log($"[reader] {input.Name}: read natively by a raw-file plugin, no conversion needed.");
                resolvedPaths.Add(input.Path);
            }
            else if (conversion.IsVendorFormat(input.Path))
            {
                reporter.Stage("Converting", input.Name, 100.0 * i / inputs.Count, 5.0 * i / inputs.Count, "Converting vendor format to mzML");
                resolvedPaths.Add(await conversion.EnsureMzmlAsync(input.Path, logProgress, ct).ConfigureAwait(false));
            }
            else
            {
                resolvedPaths.Add(input.Path);
            }
        }

        // ---- parameters & files -------------------------------------------------------
        var methodText = request.Parameters.ToMethodFileText(request.Mode);
        var methodPath = Path.Combine(outputFolder, "milx_method.txt");
        await File.WriteAllTextAsync(methodPath, methodText, ct).ConfigureAwait(false);
        var exports = new List<ExportedFile> { new("Method file", methodPath) };

        var stamp = AnalysisFileFactory.MakeRunStamp(DateTime.Now);
        var analysisFiles = AnalysisFileFactory.Create(inputs, resolvedPaths, outputFolder, stamp);
        var alignmentFile = AnalysisFileFactory.CreateAlignmentFile(outputFolder, stamp);

        return request.Mode switch
        {
            IonizationMode.GCMS => await RunGcmsAsync(request, methodText, analysisFiles, alignmentFile, outputFolder, exports, reporter, ct).ConfigureAwait(false),
            _ => await RunLcmsAsync(request, methodText, analysisFiles, alignmentFile, outputFolder, exports, reporter, ct).ConfigureAwait(false),
        };
    }

    private static void SetProjectProperty(ParameterBase param, List<AnalysisFileBean> analysisFiles, string outputFolder, bool isGcms)
    {
        var dt = DateTime.Now;
        param.ProjectFolderPath = outputFolder;
        param.ProjectFileName = $"Project-{dt:yyMMddHHmm}.mddata";
        param.FileID_ClassName = analysisFiles.ToDictionary(file => file.AnalysisFileId, file => file.AnalysisFileClass);
        param.FileID_AnalysisFileType = analysisFiles.ToDictionary(file => file.AnalysisFileId, file => file.AnalysisFileType);
        if (param.ProjectParam.AcquisitionType == AcquisitionType.None)
        {
            param.ProjectParam.AcquisitionType = AcquisitionType.DDA;
        }
        param.Ionization = isGcms ? Ionization.EI : Ionization.ESI;
        param.ProjectParam.MsdialVersionNumber = $"MIL-X (MS-DIAL {UpstreamVersion})";
        param.NumThreads = Math.Max(1, param.NumThreads);
    }

    // =====================================================================================
    // LC-MS
    // =====================================================================================
    private async Task<PipelineResult> RunLcmsAsync(PipelineRequest request, string methodText, List<AnalysisFileBean> analysisFiles, AlignmentFileBean alignmentFile,
        string outputFolder, List<ExportedFile> exports, Reporter reporter, CancellationToken ct)
    {
        var param = MethodFileParser.CreateLcmsParameter(methodText, reporter.Log);
        param.ProjectParam.MachineCategory = MachineCategory.LCMS;
        SetProjectProperty(param, analysisFiles, outputFolder, isGcms: false);

        ct.ThrowIfCancellationRequested();
        reporter.Stage("Setup", null, 20, 6, "Retention time correction");
        await Task.Run(() => RetentionTimeCorrectionStep.Prepare(analysisFiles, param, outputFolder, reporter.Log, ct), ct).ConfigureAwait(false);

        reporter.Stage("Setup", null, 40, 7, "Loading libraries");
        var libraries = await Task.Run(() => LibraryLoader.Load(param, reporter.Log), ct).ConfigureAwait(false);

        IMsdialDataStorage<MsdialLcmsParameter> storage = new MsdialLcmsDataStorage
        {
            AnalysisFiles = analysisFiles,
            AlignmentFiles = new List<AlignmentFileBean> { alignmentFile },
            IsotopeTextDB = libraries.IsotopeText,
            IupacDatabase = libraries.Iupac,
            MsdialLcmsParameter = param,
        };

        var dbStorage = DataBaseStorage.CreateEmpty();
        if (libraries.Msp is { Database.Count: > 0 } mspDB)
        {
            var annotator = new LcmsMspAnnotator(mspDB, param.MspSearchParam, param.TargetOmics, "MspDB", 1);
            dbStorage.AddMoleculeDataBase(mspDB, new List<IAnnotatorParameterPair<MoleculeDataBase>>
            {
                new MetabolomicsAnnotatorParameterPair(annotator.Save(), new AnnotationQueryFactory(annotator, param.PeakPickBaseParam, param.MspSearchParam, ignoreIsotopicPeak: true)),
            });
        }
        if (libraries.Lbm is { Database.Count: > 0 } lbmDB)
        {
            var lbmAnnotator = new LcmsMspAnnotator(lbmDB, param.LbmSearchParam, param.TargetOmics, param.LbmFilePath, 1);
            dbStorage.AddMoleculeDataBase(lbmDB, new List<IAnnotatorParameterPair<MoleculeDataBase>>
            {
                new MetabolomicsAnnotatorParameterPair(lbmAnnotator.Save(), new AnnotationQueryFactory(lbmAnnotator, param.PeakPickBaseParam, param.LbmSearchParam, ignoreIsotopicPeak: true)),
            });
        }
        if (libraries.Text is { Database.Count: > 0 } textDB)
        {
            var textAnnotator = new LcmsTextDBAnnotator(textDB, param.TextDbSearchParam, "TextDB", 2);
            dbStorage.AddMoleculeDataBase(textDB, new List<IAnnotatorParameterPair<MoleculeDataBase>>
            {
                new MetabolomicsAnnotatorParameterPair(textAnnotator.Save(), new AnnotationQueryFactory(textAnnotator, param.PeakPickBaseParam, param.TextDbSearchParam, ignoreIsotopicPeak: false)),
            });
        }
        storage.DataBaseMapper = new DataBaseMapper();
        storage.DataBases = dbStorage;
        storage.DataBases.SetDataBaseMapper(storage.DataBaseMapper);

        var projectDataStorage = new ProjectDataStorage(new ProjectParameter(DateTime.Now, outputFolder, Path.ChangeExtension(param.ProjectParam.ProjectFileName, ".mdproject")));
        projectDataStorage.AddStorage(storage);

        // ---- per-file processing ----------------------------------------------------------
        ct.ThrowIfCancellationRequested();
        var files = storage.AnalysisFiles;
        var evaluator = FacadeMatchResultEvaluator.FromDataBases(storage.DataBases);
        var annotationProcess = new StandardAnnotationProcess(storage.CreateAnnotationQueryFactoryStorage().MoleculeQueryFactories, evaluator, storage.DataBaseMapper);
        var providerFactory = new StandardDataProviderFactory(5, false);
        var process = new LcmsFileProcess(providerFactory, storage, annotationProcess, evaluator);
        await RunFilesAsync(process, files, param.NumThreads, reporter, ct).ConfigureAwait(false);

        // ---- per-file exports -------------------------------------------------------------
        reporter.Stage("Export", null, 0, 70, "Exporting peak tables");
        IAnalysisExporter<ChromatogramPeakFeatureCollection> peakMspExporter = new AnalysisMspExporter(storage.DataBaseMapper, storage.Parameter);
        var peakAccessor = new LcmsAnalysisMetadataAccessor(storage.DataBaseMapper, storage.Parameter, ExportspectraType.deconvoluted);
        var peakExporterFactory = new AnalysisCSVExporterFactory("\t");
        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];
            var peakContainer = await file.LoadChromatogramPeakFeatureCollectionAsync(ct).ConfigureAwait(false);

            var peakOutput = Path.Combine(outputFolder, file.AnalysisFileName + ".mdpeak");
            await Task.Run(() =>
            {
                using var stream = File.Open(peakOutput, FileMode.Create, FileAccess.Write);
                peakExporterFactory.CreateExporter(providerFactory, peakAccessor).Export(stream, file, peakContainer, new ExportStyle());
            }, ct).ConfigureAwait(false);
            exports.Add(new ExportedFile($"Peak table ({file.AnalysisFileName})", peakOutput));

            var mspOutput = Path.Combine(outputFolder, file.AnalysisFileName + ".mdmsp");
            await Task.Run(() =>
            {
                using var mspstream = File.Open(mspOutput, FileMode.Create, FileAccess.Write);
                peakMspExporter.Export(mspstream, file, peakContainer, new ExportStyle());
            }, ct).ConfigureAwait(false);
            exports.Add(new ExportedFile($"MS/MS spectra ({file.AnalysisFileName})", mspOutput));
            reporter.Stage("Export", file.AnalysisFileName, 100.0 * (i + 1) / files.Count, 70 + 5.0 * (i + 1) / files.Count, $"{peakContainer.Items.Count} peaks exported");
        }

        // ---- alignment ----------------------------------------------------------------------
        AlignmentFileBean? alignmentResultFile = null;
        if (storage.Parameter.TogetherWithAlignment)
        {
            ct.ThrowIfCancellationRequested();
            reporter.Stage("Alignment", null, 0, 75, "Alignment started");
            var alignProgress = new DelegateProgress<int>(p => reporter.Stage("Alignment", null, p, 75 + 0.15 * p, $"Alignment progress: {p}%", showInLog: p % 10 == 0));

            var (result, decResults) = await Task.Run(() =>
            {
                var serializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1");
                var factory = new LcmsAlignmentProcessFactory(storage, evaluator) { Progress = alignProgress };
                var aligner = factory.CreatePeakAligner();
                var r = aligner.Alignment(files, alignmentFile, serializer);
                r.Save(alignmentFile);
                var decs = LoadRepresentativeDeconvolutions(files, r.AlignmentSpotProperties).ToList();
                MsdecResultsWriter.Write(alignmentFile.SpectraFilePath, decs);
                return (r, decs);
            }, ct).ConfigureAwait(false);
            alignmentResultFile = alignmentFile;
            reporter.Stage("Alignment", null, 100, 90, $"Alignment finished: {result.AlignmentSpotProperties.Count} spots");

            ct.ThrowIfCancellationRequested();
            await Task.Run(() =>
            {
                var alignAccessor = new LcmsMetadataAccessor(storage.DataBaseMapper, storage.Parameter, false);
                IQuantValueAccessor quantAccessor = new LegacyQuantValueAccessor("Height", storage.Parameter);
                var stats = new[] { StatsValue.Average, StatsValue.Stdev };

                var alignOutput = Path.Combine(outputFolder, alignmentFile.FileName + ".mdalign");
                using (var stream = File.Open(alignOutput, FileMode.Create, FileAccess.Write))
                {
                    new AlignmentCSVExporter().Export(stream, result.AlignmentSpotProperties, decResults, files, new MulticlassFileMetaAccessor(0), alignAccessor, quantAccessor, stats);
                }
                exports.Add(new ExportedFile("Alignment table", alignOutput));

                if (storage.Parameter.IsHeightMatrixExport)
                {
                    var qaOutput = Path.Combine(outputFolder, alignmentFile.FileName + ".qa.tsv");
                    using var qaStream = File.Open(qaOutput, FileMode.Create, FileAccess.Write);
                    new AlignmentLongCSVExporter().ExportValueWithFileMetadata(
                        qaStream,
                        result.AlignmentSpotProperties,
                        files,
                        new MulticlassFileMetaAccessor(0),
                        ("Height", new LegacyQuantValueAccessor("Height", storage.Parameter)),
                        ("RT", new LegacyQuantValueAccessor("RT", storage.Parameter)),
                        ("MZ", new LegacyQuantValueAccessor("MZ", storage.Parameter)),
                        ("SN", new LegacyQuantValueAccessor("SN", storage.Parameter)),
                        ("MSMS", new LegacyQuantValueAccessor("MSMS", storage.Parameter)),
                        ("Reference matched", new LegacyQuantValueAccessor("Reference matched", storage.Parameter)));
                    exports.Add(new ExportedFile("QA matrix (long format)", qaOutput));
                }

                var alignMspOutput = Path.Combine(outputFolder, alignmentFile.FileName + ".mdmsp");
                using (var streammsp = File.Open(alignMspOutput, FileMode.Create, FileAccess.Write))
                {
                    IAlignmentSpectraExporter mspExporter = new AlignmentMspExporter(storage.DataBaseMapper, storage.Parameter);
                    mspExporter.BatchExport(streammsp, result.AlignmentSpotProperties, decResults);
                }
                exports.Add(new ExportedFile("Alignment MS/MS spectra", alignMspOutput));

                var mztabName = alignmentFile.FileName + ".mzTab";
                var mztabOutput = Path.Combine(outputFolder, mztabName);
                using (var tabmstream = File.Open(mztabOutput, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    new MztabFormatExporter(storage.DataBases).MztabFormatExporterCore(tabmstream, result.AlignmentSpotProperties, decResults, files, alignAccessor, quantAccessor, stats, mztabName);
                }
                exports.Add(new ExportedFile("mzTab-M", mztabOutput));
            }, ct).ConfigureAwait(false);
            reporter.Stage("Export", null, 100, 95, "Alignment exports written");
        }

        // ---- project ------------------------------------------------------------------------
        string? projectPath = null;
        if (request.SaveProject)
        {
            ct.ThrowIfCancellationRequested();
            reporter.Stage("Project", null, 0, 96, "Saving project");
            projectPath = await SaveProjectAsync(projectDataStorage, storage.Parameter, reporter).ConfigureAwait(false);
            if (projectPath is not null)
            {
                exports.Add(new ExportedFile("Project (.mdproject)", projectPath));
            }
        }

        return new PipelineResult(outputFolder, files, alignmentResultFile, param, storage.DataBaseMapper, exports, projectPath, IonizationMode.LCMS);
    }

    // =====================================================================================
    // GC-MS
    // =====================================================================================
    private async Task<PipelineResult> RunGcmsAsync(PipelineRequest request, string methodText, List<AnalysisFileBean> analysisFiles, AlignmentFileBean alignmentFile,
        string outputFolder, List<ExportedFile> exports, Reporter reporter, CancellationToken ct)
    {
        var param = MethodFileParser.CreateGcmsParameter(methodText, reporter.Log);
        param.ProjectParam.MachineCategory = MachineCategory.GCMS;
        SetProjectProperty(param, analysisFiles, outputFolder, isGcms: true);

        if (!string.IsNullOrEmpty(param.RiDictionaryFilePath))
        {
            if (!File.Exists(param.RiDictionaryFilePath))
            {
                throw new FileNotFoundException($"RI dictionary table '{param.RiDictionaryFilePath}' does not exist.");
            }
            ApplyRiDictionaries(analysisFiles, param, reporter.Log);
        }

        reporter.Stage("Setup", null, 40, 7, "Loading libraries");
        var libraries = await Task.Run(() => LibraryLoader.Load(param, reporter.Log), ct).ConfigureAwait(false);

        var storage = new MsdialGcmsDataStorage
        {
            AnalysisFiles = analysisFiles,
            AlignmentFiles = new List<AlignmentFileBean> { alignmentFile },
            MspDB = libraries.Msp is null ? new List<MoleculeMsReference>() : libraries.Msp.Database.ToList(),
            TextDB = libraries.Text is null ? new List<MoleculeMsReference>() : libraries.Text.Database.ToList(),
            IsotopeTextDB = libraries.IsotopeText,
            IupacDatabase = libraries.Iupac,
            MsdialGcmsParameter = param,
        };

        var dbStorage = DataBaseStorage.CreateEmpty();
        if (libraries.Msp is { Database.Count: > 0 } mspDB)
        {
            var annotator = new MassAnnotator(mspDB, param.MspSearchParam, param.TargetOmics, SourceType.MspDB, "MspDB", 1);
            dbStorage.AddMoleculeDataBase(mspDB, new List<IAnnotatorParameterPair<MoleculeDataBase>>
            {
                new MetabolomicsAnnotatorParameterPair(annotator.Save(), new AnnotationQueryFactory(annotator, param.PeakPickBaseParam, param.MspSearchParam, ignoreIsotopicPeak: true)),
            });
        }
        if (libraries.Text is { Database.Count: > 0 } textDB)
        {
            var textannotator = new MassAnnotator(textDB, param.TextDbSearchParam, param.TargetOmics, SourceType.TextDB, "TextDB", 2);
            dbStorage.AddMoleculeDataBase(textDB, new List<IAnnotatorParameterPair<MoleculeDataBase>>
            {
                new MetabolomicsAnnotatorParameterPair(textannotator.Save(), new AnnotationQueryFactory(textannotator, param.PeakPickBaseParam, param.TextDbSearchParam, ignoreIsotopicPeak: false)),
            });
        }
        storage.DataBaseMapper = new DataBaseMapper();
        storage.DataBases = dbStorage;
        storage.DataBases.SetDataBaseMapper(storage.DataBaseMapper);

        var projectDataStorage = new ProjectDataStorage(new ProjectParameter(DateTime.Now, outputFolder, Path.ChangeExtension(param.ProjectParam.ProjectFileName, ".mdproject")));
        projectDataStorage.AddStorage(storage);

        // ---- per-file processing ----------------------------------------------------------
        ct.ThrowIfCancellationRequested();
        var files = storage.AnalysisFiles;
        var metaAccessor = new GcmsAnalysisMetadataAccessor(storage.DataBaseMapper, new DelegateMsScanPropertyLoader<SpectrumFeature>(s => s.AnnotatedMSDecResult.MSDecResult));
        var providerFactory = new StandardDataProviderFactory(isGuiProcess: false);
        var process = new GcmsFileProcess(providerFactory, storage, new CalculateMatchScore(storage.DataBases.MetabolomicsDataBases.FirstOrDefault(), param.MspSearchParam, param.RetentionType));
        await RunFilesAsync(process, files, param.NumThreads, reporter, ct).ConfigureAwait(false);

        // ---- per-file exports -------------------------------------------------------------
        reporter.Stage("Export", null, 0, 70, "Exporting scan tables");
        var scanExporter = new AnalysisCSVExporterFactory("\t").CreateExporter(metaAccessor);
        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];
            var scanOutput = Path.Combine(outputFolder, file.AnalysisFileName + ".mdscan");
            await Task.Run(() =>
            {
                var sfs = file.LoadSpectrumFeatures();
                using var stream = File.Open(scanOutput, FileMode.Create, FileAccess.Write, FileShare.Read);
                scanExporter.Export(stream, file, sfs.Items, new ExportStyle());
            }, ct).ConfigureAwait(false);
            exports.Add(new ExportedFile($"Scan table ({file.AnalysisFileName})", scanOutput));
            reporter.Stage("Export", file.AnalysisFileName, 100.0 * (i + 1) / files.Count, 70 + 5.0 * (i + 1) / files.Count, "scan table exported");
        }

        // ---- alignment ----------------------------------------------------------------------
        AlignmentFileBean? alignmentResultFile = null;
        if (param.TogetherWithAlignment)
        {
            ct.ThrowIfCancellationRequested();
            reporter.Stage("Alignment", null, 0, 75, "Alignment started");
            var (result, decResults) = await Task.Run(() =>
            {
                ChromatogramSerializer<ChromatogramSpotInfo>? serializer;
                switch (param.AlignmentIndexType)
                {
                    case AlignmentIndexType.RI:
                        serializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.RI);
                        if (serializer is not null)
                        {
                            serializer = new RIChromatogramSerializerDecorator(serializer, param.GetRIHandlers());
                        }
                        break;
                    default:
                        serializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.RT);
                        break;
                }
                var factory = new GcmsAlignmentProcessFactory(storage);
                var aligner = factory.CreatePeakAligner();
                aligner.ProviderFactory = providerFactory;
                var r = aligner.Alignment(files, alignmentFile, serializer);
                r.Save(alignmentFile);
                var decs = LoadRepresentativeDeconvolutions(files, r.AlignmentSpotProperties).ToList();
                MsdecResultsWriter.Write(alignmentFile.SpectraFilePath, decs);
                return (r, decs);
            }, ct).ConfigureAwait(false);
            alignmentResultFile = alignmentFile;
            reporter.Stage("Alignment", null, 100, 90, $"Alignment finished: {result.AlignmentSpotProperties.Count} spots");

            await Task.Run(() =>
            {
                var accessor = new GcmsAlignmentMetadataAccessor(storage.DataBaseMapper, param, false);
                var quantAccessor = new LegacyQuantValueAccessor("Height", param);
                var stats = new[] { StatsValue.Average, StatsValue.Stdev };

                var alignOutput = Path.Combine(outputFolder, alignmentFile.FileName + ".mdalign");
                using (var stream = File.Open(alignOutput, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    new AlignmentCSVExporter("\t").Export(stream, result.AlignmentSpotProperties, decResults, files, new MulticlassFileMetaAccessor(0), accessor, quantAccessor, stats);
                }
                exports.Add(new ExportedFile("Alignment table", alignOutput));

                var mztabName = alignmentFile.FileName + ".mzTab";
                var mztabOutput = Path.Combine(outputFolder, mztabName);
                using (var tabmstream = File.Open(mztabOutput, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    new MztabFormatExporter(storage.DataBases).MztabFormatExporterCore(tabmstream, result.AlignmentSpotProperties, decResults, files, accessor, quantAccessor, stats, mztabName);
                }
                exports.Add(new ExportedFile("mzTab-M", mztabOutput));
            }, ct).ConfigureAwait(false);
            reporter.Stage("Export", null, 100, 95, "Alignment exports written");
        }

        string? projectPath = null;
        if (request.SaveProject)
        {
            ct.ThrowIfCancellationRequested();
            reporter.Stage("Project", null, 0, 96, "Saving project");
            projectPath = await SaveProjectAsync(projectDataStorage, param, reporter).ConfigureAwait(false);
            if (projectPath is not null)
            {
                exports.Add(new ExportedFile("Project (.mdproject)", projectPath));
            }
        }

        return new PipelineResult(outputFolder, files, alignmentResultFile, param, storage.DataBaseMapper, exports, projectPath, IonizationMode.GCMS);
    }

    private static void ApplyRiDictionaries(List<AnalysisFileBean> analysisFiles, MsdialGcmsParameter param, Action<string> log)
    {
        foreach (var line in File.ReadAllLines(param.RiDictionaryFilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split('\t');
            if (cells.Length < 2) continue;
            var file = analysisFiles.FirstOrDefault(f => string.Equals(f.AnalysisFilePath, cells[0], StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(f.AnalysisFileName, Path.GetFileNameWithoutExtension(cells[0]), StringComparison.OrdinalIgnoreCase));
            if (file is not null)
            {
                file.RiDictionaryFilePath = cells[1];
            }
        }
        var missing = analysisFiles.Where(f => f.RiDictionaryFilePath.IsEmptyOrNull()).Select(f => f.AnalysisFileName).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException("The RI dictionary file is not set for: " + string.Join(", ", missing) + ". Set an RI dictionary file for every imported file.");
        }
        param.FileIdRiInfoDictionary = new Dictionary<int, RiDictionaryInfo>();
        foreach (var file in analysisFiles)
        {
            var dictionary = RetentionIndexHandler.GetRiDictionary(file.RiDictionaryFilePath);
            if (dictionary is null || dictionary.Count == 0)
            {
                throw new InvalidDataException($"Invalid RI information in '{file.RiDictionaryFilePath}'. Expected two tab separated columns: carbon number and RT (min).");
            }
            param.FileIdRiInfoDictionary[file.AnalysisFileId] = new RiDictionaryInfo
            {
                DictionaryFilePath = file.RiDictionaryFilePath,
                RiDictionary = dictionary,
            };
            log($"RI dictionary for {file.AnalysisFileName}: {dictionary.Count} entries");
        }
    }

    // =====================================================================================
    // shared pieces
    // =====================================================================================
    private static async Task RunFilesAsync(IFileProcessor process, List<AnalysisFileBean> files, int numThreads, Reporter reporter, CancellationToken ct)
    {
        var parallel = Math.Max(1, numThreads / 2);
        var runner = new ProcessRunner(process, parallel);
        var perFile = new double[files.Count];
        var sync = new object();
        var progresses = files.Select((file, index) => (IProgress<int>?)new DelegateProgress<int>(p =>
        {
            double overall;
            lock (sync)
            {
                perFile[index] = Math.Clamp(p, 0, 100);
                overall = 10 + 60 * perFile.Sum() / (100.0 * files.Count);
            }
            reporter.Stage("Processing", file.AnalysisFileName, p, overall, $"{p}%", showInLog: p % 25 == 0);
        })).ToList();

        reporter.Stage("Processing", null, 0, 10, $"Processing {files.Count} file(s) with {parallel} worker(s)");
        var done = 0;
        await runner.RunAllAsync(files, ProcessOption.All, progresses, () =>
        {
            var n = Interlocked.Increment(ref done);
            reporter.Log($"File {n}/{files.Count} completed");
        }, ct).ConfigureAwait(false);
        reporter.Stage("Processing", null, 100, 70, "All files processed");
    }

    private static IEnumerable<MSDecResult> LoadRepresentativeDeconvolutions(IReadOnlyList<AnalysisFileBean> files, IReadOnlyList<AlignmentSpotProperty>? spots)
    {
        var pointerss = new List<(int version, List<long> pointers, bool isAnnotationInfo)>();
        foreach (var file in files)
        {
            MsdecResultsReader.GetSeekPointers(file.DeconvolutionFilePath, out var version, out var pointers, out var isAnnotationInfo);
            pointerss.Add((version, pointers, isAnnotationInfo));
        }

        var streams = new List<FileStream>();
        try
        {
            streams = files.Select(file => File.Open(file.DeconvolutionFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)).ToList();
            foreach (var spot in spots.OrEmptyIfNull())
            {
                var repID = spot.RepresentativeFileID;
                var peakID = spot.AlignedPeakProperties[repID].MasterPeakID;
                yield return MsdecResultsReader.ReadMSDecResult(streams[repID], pointerss[repID].pointers[peakID], pointerss[repID].version, pointerss[repID].isAnnotationInfo);
            }
        }
        finally
        {
            streams.ForEach(stream => stream.Close());
        }
    }

    private static async Task<string?> SaveProjectAsync(ProjectDataStorage projectDataStorage, ParameterBase parameter, Reporter reporter)
    {
        parameter.ProjectParam.FinalSavedDate = DateTime.Now;
        var path = projectDataStorage.ProjectParameter.FilePath;
        var faulted = false;
        using (var stream = File.Open(path, FileMode.Create))
        using (IStreamManager streamManager = new ZipStreamManager(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            await projectDataStorage.Save(streamManager, new MsdialIntegrateSerializer(), file => new DirectoryTreeStreamManager(file), p =>
            {
                faulted = true;
                reporter.Log($"Saving dataset {p.ProjectFileName} failed");
            }).ConfigureAwait(false);
            streamManager.Complete();
        }
        if (faulted)
        {
            return null;
        }
        reporter.Log($"Project saved: {path}");
        return path;
    }

    /// <summary>Translates stage/percent reports into <see cref="PipelineProgress"/> records.</summary>
    private sealed class Reporter
    {
        private readonly IProgress<PipelineProgress>? _progress;
        private string _stage = "Setup";
        private string? _file;
        private double _percent;
        private double _overall;

        public Reporter(IProgress<PipelineProgress>? progress) => _progress = progress;

        public void Stage(string stage, string? file, double percent, double overall, string message, bool showInLog = true)
        {
            lock (this)
            {
                _stage = stage;
                _file = file;
                _percent = percent;
                _overall = Math.Max(_overall, overall);
                _progress?.Report(new PipelineProgress(stage, file, percent, _overall, message, showInLog));
            }
        }

        public void Log(string message)
        {
            lock (this)
            {
                _progress?.Report(new PipelineProgress(_stage, _file, _percent, _overall, message, true));
            }
        }
    }
}
