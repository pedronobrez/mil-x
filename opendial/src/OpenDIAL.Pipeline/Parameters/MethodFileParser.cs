// Adapted from MS-DIAL 5 (LGPL-3.0):
//   tests/MSDIAL5/MsdialCoreTestApp/Parser/ConfigParser.cs
// Original copyright (c) RIKEN and the MS-DIAL contributors.
// Changes for OpenDIAL: parses from text instead of a file path, uses invariant
// culture for every numeric conversion, never writes to the console, fixes the
// upstream "sigma window valueLower" / "replace true zero valueLowers" key typos,
// and adds the "Number of threads" and "Gap filling by compulsion" keys.

using System.Globalization;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Parser;
using CompMs.Common.Query;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialGcMsApi.Parameter;
using CompMs.MsdialLcmsApi.Parameter;

namespace OpenDIAL.Pipeline.Parameters;

/// <summary>Builds upstream parameter objects from the console "Key: value" method-file text.</summary>
public static class MethodFileParser
{
    public static MsdialLcmsParameter CreateLcmsParameter(string methodText, Action<string>? warn = null)
    {
        var param = new MsdialLcmsParameter();
        foreach (var line in methodText.Split('\n'))
        {
            if (TryReadFieldValues(line, out var method, out var value))
            {
                ReadCommonParameter(param, method, value, warn);
            }
        }
        return param;
    }

    public static MsdialGcmsParameter CreateGcmsParameter(string methodText, Action<string>? warn = null)
    {
        var param = new MsdialGcmsParameter();
        foreach (var line in methodText.Split('\n'))
        {
            if (TryReadFieldValues(line, out var method, out var value))
            {
                if (!ReadCommonParameter(param, method, value, warn))
                {
                    ReadGcmsSpecificParameter(param, method, value);
                }
            }
        }
        if (param.AccuracyType == AccuracyType.IsNominal)
        {
            param.MassSliceWidth = 0.5F;
            param.CentroidMs1Tolerance = 0.5F;
        }
        return param;
    }

    /// <summary>Splits "Key: value" into its parts. Comment lines (#) and blank lines return false.</summary>
    public static bool TryReadFieldValues(string? line, out string method, out string value)
    {
        method = string.Empty;
        value = string.Empty;
        if (string.IsNullOrEmpty(line)) return false;
        line = line.TrimEnd('\r');
        if (line.Length < 2) return false;
        if (line[0] == '#') return false;
        var idx = line.IndexOf(':');
        if (idx < 0) return false;
        method = line.Substring(0, idx).Trim();
        value = line.Substring(idx + 1).Trim();
        return method.Length > 0;
    }

    private static bool TryFloat(string v, out float f) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    private static bool TryDouble(string v, out double d) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
    private static bool TryInt(string v, out int i) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i);
    private static bool IsBool(string v) => v == "true" || v == "false";

    public static bool ReadGcmsSpecificParameter(MsdialGcmsParameter param, string method, string value)
    {
        if (value.IsEmptyOrNull()) return false;
        if (method.IsEmptyOrNull()) return false;
        method = method.ToLowerInvariant();
        var valueLower = value.ToLowerInvariant();
        switch (method)
        {
            case "ri index file pathes": param.RiDictionaryFilePath = value; return true;
            case "retention type":
                if (valueLower == "rt" || valueLower == "ri")
                    param.RetentionType = (RetentionType)System.Enum.Parse(typeof(RetentionType), valueLower, true);
                return true;
            case "ri compound":
            case "ri compound type":
                if (valueLower == "fames" || valueLower == "alkanes")
                    param.RiCompoundType = (RiCompoundType)System.Enum.Parse(typeof(RiCompoundType), valueLower, true);
                return true;
            case "alignment index type": param.AlignmentIndexType = valueLower == "ri" ? AlignmentIndexType.RI : AlignmentIndexType.RT; return true;
            case "retention index tolerance for alignment": if (TryFloat(valueLower, out float ritolAlign)) param.RetentionIndexAlignmentTolerance = ritolAlign; return true;
            case "replace quant mass by user defined value": if (valueLower == "true") param.IsReplaceQuantmassByUserDefinedValue = true; return true;
            case "is quant mass based on base peak mz": if (valueLower == "true") param.IsRepresentativeQuantMassBasedOnBasePeakMz = true; return true;
            default: return false;
        }
    }

    public static bool ReadCommonParameter(ParameterBase param, string method, string value, Action<string>? warn = null)
    {
        if (value.IsEmptyOrNull()) return false;
        if (method.IsEmptyOrNull()) return false;
        method = method.ToLowerInvariant();
        var valueLower = value.ToLowerInvariant();
        switch (method)
        {
            //Data type
            case "ms1 data type":
                if (valueLower == "centroid" || valueLower == "profile")
                    param.MSDataType = (MSDataType)System.Enum.Parse(typeof(MSDataType), valueLower, true);
                return true;
            case "ms2 data type":
                if (valueLower == "centroid" || valueLower == "profile")
                    param.MS2DataType = (MSDataType)System.Enum.Parse(typeof(MSDataType), valueLower, true);
                return true;
            case "ion mode":
                if (valueLower == "positive" || valueLower == "negative")
                    param.IonMode = (IonMode)System.Enum.Parse(typeof(IonMode), valueLower, true);
                return true;
            case "target omics":
                if (valueLower == "metabolomics" || valueLower == "lipidomics")
                    param.TargetOmics = (TargetOmics)System.Enum.Parse(typeof(TargetOmics), valueLower, true);
                return true;
            case "acquisition type":
                if (valueLower == "dda" || valueLower == "swath" || valueLower == "aif")
                    param.ProjectParam.AcquisitionType = (AcquisitionType)System.Enum.Parse(typeof(AcquisitionType), valueLower, true);
                return true;
            case "machine category":
                if (value == "GCMS" || value == "LCMS" || value == "IMMS" || value == "LCIMMS" || value == "IFMS" || value == "IIMMS" || value == "IDIMS")
                    param.ProjectParam.MachineCategory = (MachineCategory)System.Enum.Parse(typeof(MachineCategory), valueLower, true);
                return true;
            case "solvent type":
                if (value == "CH3COONH4" || value == "HCOONH4" || value == "NH4HCO3")
                    param.LipidQueryContainer.SolventType = (SolventType)System.Enum.Parse(typeof(SolventType), valueLower, true);
                return true;
            case "searched lipid class":
                if (!value.IsEmptyOrNull())
                {
                    param.LipidQueryContainer = new LipidQueryBean()
                    {
                        SolventType = SolventType.CH3COONH4,
                        LbmQueries = LbmQueryParcer.GetLbmQueries(isLabUseOnly: param.IsLabPrivate)
                    };
                    param.LipidQueryContainer.LbmQueries = param.LipidQueryContainer.LbmQueries.Where(n => n.IonMode == param.IonMode).ToList();
                    foreach (var l in param.LipidQueryContainer.LbmQueries) l.IsSelected = false;

                    foreach (var lipidString in value.Split(';'))
                    {
                        var parts = lipidString.Split(' ');
                        if (parts.Length < 2) continue;
                        var lipidclass = parts[0];
                        var adducttype = parts[1];
                        if (!System.Enum.IsDefined(typeof(LbmClass), lipidclass)) continue;
                        var adductObj = AdductIon.GetAdductIon(adducttype);
                        if (!adductObj.FormatCheck) continue;
                        foreach (var l in param.LipidQueryContainer.LbmQueries)
                        {
                            if (l.LbmClass.ToString() == lipidclass && adductObj.ToString() == l.AdductType.ToString())
                            {
                                l.IsSelected = true;
                                break;
                            }
                        }
                    }
                }
                return true;

            //File paths
            case "msp file path": param.MspFilePath = value; return true;
            case "lbm file path": param.LbmFilePath = value; return true;
            case "text db file path": param.TextDBFilePath = value; return true;
            case "isotope text db file path": param.IsotopeTextDBFilePath = value; return true;
            case "compounds library file path for target detection": param.CompoundListInTargetModePath = value; return true;
            case "compounds library file path for rt correction":
                param.CompoundListForRtCorrectionPath = value;
                if (File.Exists(value))
                {
                    param.RetentionTimeCorrectionCommon.StandardLibrary = TextLibraryParser.StandardTextLibraryReader(value, out var error);
                    if (!string.IsNullOrEmpty(error)) warn?.Invoke(error);
                }
                return true;
            case "rt correction peak selection file path": param.ReferenceFileParam.RtCorrectionPeakSelectionFilePath = value; return true;

            // Private version
            case "is private version of tada": if (valueLower == "true") param.IsLabPrivateVersionTada = true; return true;
            case "is private version": if (valueLower == "true") param.IsLabPrivate = true; return true;

            //Data correction
            case "retention time begin": if (TryFloat(valueLower, out float rtbegin)) param.RetentionTimeBegin = rtbegin; return true;
            case "retention time end": if (TryFloat(valueLower, out float rtend)) param.RetentionTimeEnd = rtend; return true;
            case "ms1 mass range begin": if (TryFloat(valueLower, out float ms1begin)) param.MassRangeBegin = ms1begin; return true;
            case "ms1 mass range end": if (TryFloat(valueLower, out float ms1end)) param.MassRangeEnd = ms1end; return true;
            case "ms2 mass range begin": if (TryFloat(valueLower, out float ms2begin)) param.Ms2MassRangeBegin = ms2begin; return true;
            case "ms2 mass range end": if (TryFloat(valueLower, out float ms2end)) param.Ms2MassRangeEnd = ms2end; return true;
            case "accuracy type":
                if (valueLower == "isnominal" || valueLower == "isaccurate")
                    param.AccuracyType = (AccuracyType)System.Enum.Parse(typeof(AccuracyType), valueLower, true);
                return true;

            //Centroid parameters
            case "ms1 tolerance for centroid": if (TryFloat(valueLower, out float centMs1Tol)) param.CentroidMs1Tolerance = centMs1Tol; return true;
            case "ms2 tolerance for centroid": if (TryFloat(valueLower, out float centMs2Tol)) param.CentroidMs2Tolerance = centMs2Tol; return true;

            //Peak detection param
            case "smoothing method":
                if (System.Enum.TryParse(value, true, out SmoothingMethod smoothingMethod))
                    param.SmoothingMethod = smoothingMethod;
                return true;
            case "smoothing level": if (TryInt(valueLower, out int smoothlevel)) param.SmoothingLevel = smoothlevel; return true;
            case "average peak width": if (TryInt(valueLower, out int avepeakwidth)) param.AveragePeakWidth = avepeakwidth; return true;
            case "minimum peak width": if (TryInt(valueLower, out int minpeakwidth)) param.MinimumDatapoints = minpeakwidth; return true;
            case "minimum peak height": if (TryInt(valueLower, out int minpeakheight)) param.MinimumAmplitude = minpeakheight; return true;
            case "mass slice width": if (TryFloat(valueLower, out float massSliceWidth)) param.MassSliceWidth = massSliceWidth; return true;
            case "mass accuracy": if (TryFloat(valueLower, out float ms1accuracy)) param.CentroidMs1Tolerance = ms1accuracy; return true;
            case "max charge number": if (TryInt(valueLower, out int maxchargenum)) param.MaxChargeNumber = maxchargenum; return true;
            case "searched adduct ions":
                if (!value.IsEmptyOrNull())
                {
                    param.SearchedAdductIons = new List<AdductIon>();
                    foreach (var adductString in value.Split(','))
                    {
                        var adductObj = AdductIon.GetAdductIon(adductString.Trim());
                        if (adductObj.FormatCheck) param.SearchedAdductIons.Add(adductObj);
                    }
                }
                return true;

            //Deconvolution
            case "sigma window value":
            case "sigma window valuelower": if (TryFloat(valueLower, out float sigmaWindow)) param.SigmaWindowValue = sigmaWindow; return true;
            case "amplitude cut off": if (TryFloat(valueLower, out float ms2ampthreshold)) param.ChromDecBaseParam.AmplitudeCutoff = ms2ampthreshold; return true;
            case "relative amplitude cut off": if (TryFloat(valueLower, out float ms2relativeampthreshold)) param.ChromDecBaseParam.RelativeAmplitudeCutoff = ms2relativeampthreshold; return true;
            case "keep isotope range": if (TryFloat(valueLower, out float keepisotoperange)) param.KeptIsotopeRange = keepisotoperange; return true;
            case "exclude after precursor": if (valueLower == "false") param.RemoveAfterPrecursor = false; return true;
            case "keep original precursor isotopes": if (valueLower == "false") param.KeepOriginalPrecursorIsotopes = false; return true;
            case "target ce": if (TryDouble(valueLower, out double targetce)) param.TargetCE = targetce; return true;

            //Identification (MSP)
            case "rt tolerance for msp-based annotation": if (TryFloat(valueLower, out float rttolIdent)) param.MspSearchParam.RtTolerance = rttolIdent; return true;
            case "ri tolerance for msp-based annotation": if (TryFloat(valueLower, out float ritolIdent)) param.MspSearchParam.RiTolerance = ritolIdent; return true;
            case "ccs tolerance for msp-based annotation": if (TryFloat(valueLower, out float ccstolIdent)) param.MspSearchParam.CcsTolerance = ccstolIdent; return true;
            case "mass range begin for msp-based annotation": if (TryFloat(valueLower, out float msbeginIdent)) param.MspSearchParam.MassRangeBegin = msbeginIdent; return true;
            case "mass range end for msp-based annotation": if (TryFloat(valueLower, out float msendIdent)) param.MspSearchParam.MassRangeEnd = msendIdent; return true;
            case "relative amplitude cutoff for msp-based annotation": if (TryFloat(valueLower, out float relampIdent)) param.MspSearchParam.RelativeAmpCutoff = relampIdent; return true;
            case "absolute amplitude cutoff for msp-based annotation": if (TryFloat(valueLower, out float absampIdent)) param.MspSearchParam.AbsoluteAmpCutoff = absampIdent; return true;
            case "weighted dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float sqdotproduct)) param.MspSearchParam.SquaredWeightedDotProductCutOff = sqdotproduct; return true;
            case "simple dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float sqsimpleproduct)) param.MspSearchParam.SquaredSimpleDotProductCutOff = sqsimpleproduct; return true;
            case "reverse dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float sqrevdotproduct)) param.MspSearchParam.SquaredReverseDotProductCutOff = sqrevdotproduct; return true;
            case "square root of weighted dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float dotproduct)) param.MspSearchParam.WeightedDotProductCutOff = dotproduct; return true;
            case "square root of simple dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float simpleproduct)) param.MspSearchParam.SimpleDotProductCutOff = simpleproduct; return true;
            case "square root of reverse dot product cutoff for msp-based annotation": if (TryFloat(valueLower, out float revdotproduct)) param.MspSearchParam.ReverseDotProductCutOff = revdotproduct; return true;
            case "matched peaks percentage cutoff for msp-based annotation": if (TryFloat(valueLower, out float matchedpeakspercent)) param.MspSearchParam.MatchedPeaksPercentageCutOff = matchedpeakspercent; return true;
            case "minimum spectrum match for msp-based annotation": if (TryFloat(valueLower, out float minpeakmatch)) param.MspSearchParam.MinimumSpectrumMatch = minpeakmatch; return true;
            case "total score cutoff for msp-based annotation": if (TryFloat(valueLower, out float cutoffIdent)) param.MspSearchParam.TotalScoreCutoff = cutoffIdent; return true;
            case "ms1 tolerance for msp-based annotation": if (TryFloat(valueLower, out float ms1tolIdent)) param.MspSearchParam.Ms1Tolerance = ms1tolIdent; return true;
            case "ms2 tolerance for msp-based annotation": if (TryFloat(valueLower, out float ms2tolIdent)) param.MspSearchParam.Ms2Tolerance = ms2tolIdent; return true;
            case "use retention information for msp-based annotation scoring": if (IsBool(valueLower)) param.MspSearchParam.IsUseTimeForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use retention information for msp-based annotation filtering": if (IsBool(valueLower)) param.MspSearchParam.IsUseTimeForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "use ccs for msp-based annotation scoring": if (IsBool(valueLower)) param.MspSearchParam.IsUseCcsForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use ccs for msp-based annotation filtering": if (IsBool(valueLower)) param.MspSearchParam.IsUseCcsForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "only report top hit for msp-based annotation": if (IsBool(valueLower)) param.OnlyReportTopHitInMspSearch = bool.Parse(valueLower); return true;
            case "execute annotation process only for alignment file for msp-based annotation": if (IsBool(valueLower)) param.IsIdentificationOnlyPerformedForAlignmentFile = bool.Parse(valueLower); return true;

            //Identification (LBM)
            case "rt tolerance for lbm-based annotation": if (TryFloat(valueLower, out float rttolLbm)) param.LbmSearchParam.RtTolerance = rttolLbm; return true;
            case "ri tolerance for lbm-based annotation": if (TryFloat(valueLower, out float ritolLbm)) param.LbmSearchParam.RiTolerance = ritolLbm; return true;
            case "ccs tolerance for lbm-based annotation": if (TryFloat(valueLower, out float ccstolLbm)) param.LbmSearchParam.CcsTolerance = ccstolLbm; return true;
            case "mass range begin for lbm-based annotation": if (TryFloat(valueLower, out float msbeginLbm)) param.LbmSearchParam.MassRangeBegin = msbeginLbm; return true;
            case "mass range end for lbm-based annotation": if (TryFloat(valueLower, out float msendLbm)) param.LbmSearchParam.MassRangeEnd = msendLbm; return true;
            case "relative amplitude cutoff for lbm-based annotation": if (TryFloat(valueLower, out float relampLbm)) param.LbmSearchParam.RelativeAmpCutoff = relampLbm; return true;
            case "absolute amplitude cutoff for lbm-based annotation": if (TryFloat(valueLower, out float absampLbm)) param.LbmSearchParam.AbsoluteAmpCutoff = absampLbm; return true;
            case "weighted dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmSqdot)) param.LbmSearchParam.SquaredWeightedDotProductCutOff = lbmSqdot; return true;
            case "simple dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmSqsimple)) param.LbmSearchParam.SquaredSimpleDotProductCutOff = lbmSqsimple; return true;
            case "reverse dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmSqrev)) param.LbmSearchParam.SquaredReverseDotProductCutOff = lbmSqrev; return true;
            case "square root of weighted dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmDot)) param.LbmSearchParam.WeightedDotProductCutOff = lbmDot; return true;
            case "square root of simple dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmSimple)) param.LbmSearchParam.SimpleDotProductCutOff = lbmSimple; return true;
            case "square root of reverse dot product cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmRev)) param.LbmSearchParam.ReverseDotProductCutOff = lbmRev; return true;
            case "matched peaks percentage cutoff for lbm-based annotation": if (TryFloat(valueLower, out float lbmMatched)) param.LbmSearchParam.MatchedPeaksPercentageCutOff = lbmMatched; return true;
            case "minimum spectrum match for lbm-based annotation": if (TryFloat(valueLower, out float lbmMinmatch)) param.LbmSearchParam.MinimumSpectrumMatch = lbmMinmatch; return true;
            case "total score cutoff for lbm-based annotation": if (TryFloat(valueLower, out float cutoffLbm)) param.LbmSearchParam.TotalScoreCutoff = cutoffLbm; return true;
            case "ms1 tolerance for lbm-based annotation": if (TryFloat(valueLower, out float ms1tolLbm)) param.LbmSearchParam.Ms1Tolerance = ms1tolLbm; return true;
            case "ms2 tolerance for lbm-based annotation": if (TryFloat(valueLower, out float ms2tolLbm)) param.LbmSearchParam.Ms2Tolerance = ms2tolLbm; return true;
            case "use retention information for lbm-based annotation scoring": if (IsBool(valueLower)) param.LbmSearchParam.IsUseTimeForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use retention information for lbm-based annotation filtering": if (IsBool(valueLower)) param.LbmSearchParam.IsUseTimeForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "use ccs for lbm-based annotation scoring": if (IsBool(valueLower)) param.LbmSearchParam.IsUseCcsForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use ccs for lbm-based annotation filtering": if (IsBool(valueLower)) param.LbmSearchParam.IsUseCcsForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "execute annotation process only for alignment file for lbm-based annotation": if (IsBool(valueLower)) param.IsIdentificationOnlyPerformedForAlignmentFile = bool.Parse(valueLower); return true;

            //Post identification (text DB)
            case "rt tolerance for text-based annotation": if (TryFloat(valueLower, out float rttolText)) param.TextDbSearchParam.RtTolerance = rttolText; return true;
            case "ri tolerance for text-based annotation": if (TryFloat(valueLower, out float ritolText)) param.TextDbSearchParam.RiTolerance = ritolText; return true;
            case "ccs tolerance for text-based annotation": if (TryFloat(valueLower, out float ccstolText)) param.TextDbSearchParam.CcsTolerance = ccstolText; return true;
            case "total score cutoff for text-based annotation": if (TryFloat(valueLower, out float cutoffText)) param.TextDbSearchParam.TotalScoreCutoff = cutoffText; return true;
            case "accurate ms1 tolerance for text-based annotation": if (TryFloat(valueLower, out float ms1tolText)) param.TextDbSearchParam.Ms1Tolerance = ms1tolText; return true;
            case "use retention information for text-based annotation scoring": if (IsBool(valueLower)) param.TextDbSearchParam.IsUseTimeForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use retention information for text-based annotation filtering": if (IsBool(valueLower)) param.TextDbSearchParam.IsUseTimeForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "use ccs for text-based annotation scoring": if (IsBool(valueLower)) param.TextDbSearchParam.IsUseCcsForAnnotationScoring = bool.Parse(valueLower); return true;
            case "use ccs for text-based annotation filtering": if (IsBool(valueLower)) param.TextDbSearchParam.IsUseCcsForAnnotationFiltering = bool.Parse(valueLower); return true;
            case "only report top hit for text-based annotation": if (IsBool(valueLower)) param.OnlyReportTopHitInTextDBSearch = bool.Parse(valueLower); return true;

            //Alignment parameters setting
            case "alignment reference file id": if (TryInt(valueLower, out int refID)) param.AlignmentReferenceFileID = refID; return true;
            case "retention time tolerance for alignment": if (TryFloat(valueLower, out float rttolAlign)) param.RetentionTimeAlignmentTolerance = rttolAlign; return true;
            case "retention time factor for alignment": if (TryFloat(valueLower, out float rtfactorAlign)) param.RetentionTimeAlignmentFactor = rtfactorAlign; return true;
            case "spectrum similarity tolerance for alignment": if (TryFloat(valueLower, out float specsimAlign)) param.SpectrumSimilarityAlignmentTolerance = specsimAlign; return true;
            case "spectrum similarity factor for alignment": if (TryFloat(valueLower, out float specsimfactorAlign)) param.SpectrumSimilarityAlignmentFactor = specsimfactorAlign; return true;
            case "ms1 tolerance for alignment": if (TryFloat(valueLower, out float ms1aligntol)) param.Ms1AlignmentTolerance = ms1aligntol; return true;
            case "ms1 factor for alignment": if (TryFloat(valueLower, out float ms1alignfactor)) param.Ms1AlignmentFactor = ms1alignfactor; return true;
            case "gap filling by compulsion":
            case "force insert peaks in gap filling": if (IsBool(valueLower)) param.IsForceInsertForGapFilling = bool.Parse(valueLower); return true;
            case "together with alignment": if (IsBool(valueLower)) param.TogetherWithAlignment = bool.Parse(valueLower); return true;

            //Filtering
            case "peak count filter": if (TryFloat(valueLower, out float peakcountfilter)) param.PeakCountFilter = peakcountfilter; return true;
            case "n percent detected in one group": if (TryFloat(valueLower, out float nPercent)) param.NPercentDetectedInOneGroup = nPercent; return true;
            case "remove feature based on peak height fold-change": if (IsBool(valueLower)) param.IsRemoveFeatureBasedOnBlankPeakHeightFoldChange = bool.Parse(valueLower); return true;
            case "blank filtering":
                if (valueLower == "samplemaxoverblankave")
                    param.BlankFiltering = (BlankFiltering)System.Enum.Parse(typeof(BlankFiltering), valueLower, true);
                return true;
            case "sample max / blank average": if (TryFloat(valueLower, out float sampleMaxOverBlankAverage)) param.SampleMaxOverBlankAverage = sampleMaxOverBlankAverage; return true;
            case "sample average / blank average": if (TryFloat(valueLower, out float sampleAverageOverBlankAverage)) param.SampleAverageOverBlankAverage = sampleAverageOverBlankAverage; return true;
            case "keep reference matched metabolites": if (IsBool(valueLower)) param.IsKeepRefMatchedMetaboliteFeatures = bool.Parse(valueLower); return true;
            case "keep suggested metabolites": if (IsBool(valueLower)) param.IsKeepSuggestedMetaboliteFeatures = bool.Parse(valueLower); return true;
            case "keep removable features and assigned tag for checking": if (IsBool(valueLower)) param.IsKeepRemovableFeaturesAndAssignedTagForChecking = bool.Parse(valueLower); return true;
            case "replace true zero values with 1/2 of minimum peak height over all samples":
            case "replace true zero valuelowers with 1/2 of minimum peak height over all samples": if (IsBool(valueLower)) param.IsReplaceTrueZeroValuesWithHalfOfMinimumPeakHeightOverAllSamples = bool.Parse(valueLower); return true;

            //Retention time correction
            case "execute rt correction": if (IsBool(valueLower)) param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.ExcuteRtCorrection = bool.Parse(valueLower); return true;
            case "rt correction with smoothing for rt diff": if (IsBool(valueLower)) param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.doSmoothing = bool.Parse(valueLower); return true;
            case "user setting intercept": if (TryFloat(valueLower, out float userintercept)) param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.UserSettingIntercept = userintercept; return true;
            case "rt diff calc method":
                if (valueLower == "sampleminussampleaverage" || valueLower == "sampleminusreference")
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.RtDiffCalcMethod = (RtDiffCalcMethod)System.Enum.Parse(typeof(RtDiffCalcMethod), valueLower, true);
                return true;
            case "interpolation method":
                if (valueLower == "linear")
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.InterpolationMethod = InterpolationMethod.Linear;
                return true;
            case "extrapolation method (begin)":
                if (valueLower == "usersetting" || valueLower == "firstpoint" || valueLower == "linearextrapolation")
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.ExtrapolationMethodBegin = (ExtrapolationMethodBegin)System.Enum.Parse(typeof(ExtrapolationMethodBegin), valueLower, true);
                return true;
            case "extrapolation method (end)":
                if (valueLower == "lastpoint" || valueLower == "linearextrapolation")
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.ExtrapolationMethodEnd = (ExtrapolationMethodEnd)System.Enum.Parse(typeof(ExtrapolationMethodEnd), valueLower, true);
                return true;
            case "rt correction peak selection mode":
                if (System.Enum.TryParse(valueLower, true, out RetentionTimeCorrectionPeakSelectionMode peakSelectionMode))
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.PeakSelectionMode = peakSelectionMode;
                return true;
            case "rt correction peak selection rt weight":
                if (TryDouble(valueLower, out var rtWeight) && rtWeight >= 0d && rtWeight <= 1d)
                    param.RetentionTimeCorrectionCommon.RetentionTimeCorrectionParam.PeakSelectionRtWeight = rtWeight;
                return true;

            //Isotope tracking setting
            case "tracking isotope label": if (IsBool(valueLower)) param.TrackingIsotopeLabels = bool.Parse(valueLower); return true;
            case "set fully labeled reference file": if (IsBool(valueLower)) param.SetFullyLabeledReferenceFile = bool.Parse(valueLower); return true;
            case "non labeled reference id": if (TryInt(valueLower, out int nonlabeledrefid)) param.NonLabeledReferenceID = nonlabeledrefid; return true;
            case "fully labeled reference id": if (TryInt(valueLower, out int fulllabeledrefid)) param.FullyLabeledReferenceID = fulllabeledrefid; return true;
            case "isotope tracking dictionary id": if (TryInt(valueLower, out int isotopetrackdictionaryid)) param.IsotopeTrackingDictionary.SelectedID = isotopetrackdictionaryid; return true;

            //CorrDec settings
            case "corrdec execute": if (valueLower == "false") param.CorrDecParam.CanExcute = false; return true;
            case "corrdec ms2 tolerance": if (TryFloat(valueLower, out float corrdecms2tol)) param.CorrDecParam.MS2Tolerance = corrdecms2tol; return true;
            case "corrdec minimum ms2 peak height": if (TryInt(valueLower, out int corrdecminms2int)) param.CorrDecParam.MinMS2Intensity = corrdecminms2int; return true;
            case "corrdec minimum number of detected samples": if (TryInt(valueLower, out int corrdecminnumberofsample)) param.CorrDecParam.MinNumberOfSample = corrdecminnumberofsample; return true;
            case "corrdec exclude highly correlated spots": if (TryFloat(valueLower, out float corrdecmincorrMs1)) param.CorrDecParam.MinCorr_MS1 = corrdecmincorrMs1; return true;
            case "corrdec minimum correlation coefficient (ms2)": if (TryFloat(valueLower, out float corrdecmincorrMs2)) param.CorrDecParam.MinCorr_MS2 = corrdecmincorrMs2; return true;
            case "corrdec margin 1 (target precursor)": if (TryFloat(valueLower, out float corrdeccorrdiffMs1)) param.CorrDecParam.CorrDiff_MS1 = corrdeccorrdiffMs1; return true;
            case "corrdec margin 2 (coeluted precursor)": if (TryFloat(valueLower, out float corrdeccorrdiffMs2)) param.CorrDecParam.CorrDiff_MS2 = corrdeccorrdiffMs2; return true;
            case "corrdec minimum detected rate": if (TryFloat(valueLower, out float corrdecmindetectedrate)) param.CorrDecParam.MinDetectedPercentToVisualize = corrdecmindetectedrate; return true;
            case "corrdec minimum ms2 relative intensity": if (TryFloat(valueLower, out float corrdecminms2relativeint)) param.CorrDecParam.MinMS2RelativeIntensity = corrdecminms2relativeint; return true;
            case "corrdec remove peaks larger than precursor": if (IsBool(valueLower)) param.CorrDecParam.CorrDecRemoveAfterPrecursor = bool.Parse(valueLower); return true;

            //Export / process (OpenDIAL extensions)
            case "is height matrix export":
            case "height matrix export": if (IsBool(valueLower)) param.IsHeightMatrixExport = bool.Parse(valueLower); return true;
            case "number of threads": if (TryInt(valueLower, out int threads) && threads > 0) param.NumThreads = threads; return true;

            default: return false;
        }
    }
}
