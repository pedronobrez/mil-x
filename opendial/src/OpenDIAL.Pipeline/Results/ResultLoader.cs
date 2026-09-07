using System.Collections.Concurrent;
using System.Globalization;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialCore.Utility;
using OpenDIAL.Pipeline.Internal;

namespace OpenDIAL.Pipeline.Results;

/// <summary>Reads processed results (peak tables, deconvoluted spectra, EICs, alignment) back from the upstream files.</summary>
public static class ResultLoader
{
    private sealed record DclIndex(int Version, List<long> Pointers, bool IsAnnotationInfo, DateTime Stamp);
    private static readonly ConcurrentDictionary<string, DclIndex> DclIndexCache = new();

    // ------------------------------------------------------------------ peaks

    public static async Task<IReadOnlyList<PeakFeatureRow>> LoadPeakTableAsync(AnalysisFileBean file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var collection = await file.LoadChromatogramPeakFeatureCollectionAsync(ct).ConfigureAwait(false);
        return collection.Items.Select(ToRow).ToList();
    }

    public static PeakFeatureRow ToRow(ChromatogramPeakFeature f)
    {
        var match = f.MatchResults?.Representative;
        var score = match is null || match.IsUnknown ? 0 : match.TotalScore;
        var isotope = f.PeakCharacter?.IsotopeWeightNumber ?? -1;
        return new PeakFeatureRow(f)
        {
            Id = f.MasterPeakID,
            Name = f.Name ?? string.Empty,
            Rt = f.ChromXsTop?.RT?.Value ?? 0,
            RtLeft = f.ChromXsLeft?.RT?.Value ?? 0,
            RtRight = f.ChromXsRight?.RT?.Value ?? 0,
            Mz = f.Mass,
            Height = f.PeakHeightTop,
            Area = f.PeakAreaAboveZero,
            Adduct = f.AdductType?.AdductIonName ?? string.Empty,
            Isotope = isotope < 0 ? string.Empty : isotope == 0 ? "M" : $"M+{isotope}",
            Score = score,
            SignalToNoise = f.PeakShape?.SignalToNoise ?? 0,
            HasMs2 = f.IsMsmsContained,
            MatchResult = match is null || match.IsUnknown ? null : match,
        };
    }

    // ------------------------------------------------------------------ MS/MS

    /// <summary>Deconvoluted MS/MS spectrum of a peak, read from the file's .dcl.</summary>
    public static MsSpectrum? LoadMs2(AnalysisFileBean file, PeakFeatureRow peak)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(peak);
        var index = peak.Feature.MSDecResultIdUsed >= 0 ? peak.Feature.MSDecResultIdUsed : peak.Id;
        var dec = ReadMsDec(file.DeconvolutionFilePath, index, peak.Feature.SeekPointToDCLFile);
        if (dec is null)
        {
            return null;
        }
        return ToSpectrum(dec, $"{peak.Name} (ID {peak.Id}, m/z {peak.Mz:F4})");
    }

    /// <summary>Representative deconvoluted spectrum of an alignment spot, read from the alignment .dcl.</summary>
    public static MsSpectrum? LoadAlignmentMs2(AlignmentFileBean alignmentFile, AlignmentSpotRow spot)
    {
        ArgumentNullException.ThrowIfNull(alignmentFile);
        ArgumentNullException.ThrowIfNull(spot);
        if (string.IsNullOrEmpty(alignmentFile.SpectraFilePath) || !File.Exists(alignmentFile.SpectraFilePath))
        {
            return null;
        }
        var dec = ReadMsDec(alignmentFile.SpectraFilePath, spot.Id, -1);
        return dec is null ? null : ToSpectrum(dec, $"{spot.Name} (spot {spot.Id}, m/z {spot.Mz:F4})");
    }

    /// <summary>Representative deconvoluted result (peaks + metadata) of an alignment spot, or null when absent.</summary>
    public static MSDecResult? LoadAlignmentMsDec(AlignmentFileBean alignmentFile, int spotId)
    {
        ArgumentNullException.ThrowIfNull(alignmentFile);
        if (string.IsNullOrEmpty(alignmentFile.SpectraFilePath) || !File.Exists(alignmentFile.SpectraFilePath))
        {
            return null;
        }
        return ReadMsDec(alignmentFile.SpectraFilePath, spotId, -1);
    }

    public static MsSpectrum ToSpectrum(MSDecResult dec, string label)
    {
        var peaks = dec.Spectrum
            .Where(p => p.Intensity > 0)
            .Select(p => new SpectrumPeakPoint(p.Mass, p.Intensity))
            .OrderBy(p => p.Mz)
            .ToList();
        return new MsSpectrum(label, dec.PrecursorMz, peaks);
    }

    private static MSDecResult? ReadMsDec(string dclPath, int index, long seekPointHint)
    {
        if (string.IsNullOrEmpty(dclPath) || !File.Exists(dclPath))
        {
            return null;
        }
        var stamp = File.GetLastWriteTimeUtc(dclPath);
        var idx = DclIndexCache.GetOrAdd(dclPath, _ => Build(dclPath, stamp));
        if (idx.Stamp != stamp)
        {
            idx = Build(dclPath, stamp);
            DclIndexCache[dclPath] = idx;
        }
        long seek;
        if (index >= 0 && index < idx.Pointers.Count)
        {
            seek = idx.Pointers[index];
        }
        else if (seekPointHint > 0)
        {
            seek = seekPointHint;
        }
        else
        {
            return null;
        }
        return MsdecResultsReader.ReadMSDecResult(dclPath, seek, idx.Version, idx.IsAnnotationInfo);

        static DclIndex Build(string path, DateTime stamp)
        {
            MsdecResultsReader.GetSeekPointers(path, out var version, out var pointers, out var isAnnotationInfo);
            return new DclIndex(version, pointers, isAnnotationInfo, stamp);
        }
    }

    // ------------------------------------------------------------------ raw data / EIC

    /// <summary>Loads all spectra of a raw file through the upstream reader (mzML, ABF, ... or converted vendor data).</summary>
    public static Task<RawMeasurement> LoadRawMeasurementAsync(AnalysisFileBean file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var raw = DataAccess.LoadMeasurement(file, isImagingMsData: false, isGuiProcess: false, retry: 5, sleepMilliSeconds: 500);
            if (raw?.SpectrumList is null)
            {
                throw new InvalidDataException($"No spectra could be read from {file.AnalysisFilePath}");
            }
            return raw;
        }, ct);
    }

    /// <summary>Extracted ion chromatogram over MS1 scans: sum of intensities within ±tolerance of mz.</summary>
    public static Chromatogram LoadEic(RawMeasurement raw, double mz, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var points = new List<ChromatogramPoint>();
        foreach (var spectrum in raw.SpectrumList)
        {
            if (spectrum.MsLevel != 1 || spectrum.Spectrum is null)
            {
                continue;
            }
            points.Add(new ChromatogramPoint(ToMinutes(spectrum), SumIntensity(spectrum.Spectrum, mz - tolerance, mz + tolerance)));
        }
        return new Chromatogram($"EIC m/z {mz:F4} ± {tolerance:F4}", mz, tolerance, points);
    }

    /// <summary>Total ion chromatogram of the MS1 scans.</summary>
    public static Chromatogram LoadTic(RawMeasurement raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var points = raw.SpectrumList
            .Where(s => s.MsLevel == 1)
            .Select(s => new ChromatogramPoint(ToMinutes(s), s.TotalIonCurrent > 0 ? s.TotalIonCurrent : (s.Spectrum?.Sum(p => p.Intensity) ?? 0)))
            .ToList();
        return new Chromatogram("TIC", 0, 0, points);
    }

    /// <summary>The raw (not deconvoluted) MS/MS scan that triggered a DDA peak, if any.</summary>
    public static MsSpectrum? LoadRawMs2(RawMeasurement raw, PeakFeatureRow peak)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var id = peak.Feature.MS2RawSpectrumID;
        if (id < 0 || id >= raw.SpectrumList.Count)
        {
            return null;
        }
        var s = raw.SpectrumList[id];
        if (s.Spectrum is null)
        {
            return null;
        }
        return new MsSpectrum($"Raw MS/MS scan {id}", s.Precursor?.SelectedIonMz ?? peak.Mz,
            s.Spectrum.Where(p => p.Intensity > 0).Select(p => new SpectrumPeakPoint(p.Mz, p.Intensity)).ToList());
    }

    private static double ToMinutes(RawSpectrum s)
        => s.ScanStartTimeUnit == Units.Second ? s.ScanStartTime / 60.0 : s.ScanStartTime;

    private static double SumIntensity(RawPeakElement[] spectrum, double lo, double hi)
    {
        // spectra are sorted by m/z; binary search for the first element >= lo
        int left = 0, right = spectrum.Length;
        while (left < right)
        {
            var mid = (left + right) >> 1;
            if (spectrum[mid].Mz < lo) left = mid + 1; else right = mid;
        }
        double sum = 0;
        for (var i = left; i < spectrum.Length && spectrum[i].Mz <= hi; i++)
        {
            sum += spectrum[i].Intensity;
        }
        return sum;
    }

    // ------------------------------------------------------------------ references

    /// <summary>Library spectrum for an annotated peak, resolved through the project's DataBaseMapper.</summary>
    public static MsSpectrum? LoadReferenceSpectrum(DataBaseMapper? mapper, MsScanMatchResult? match)
    {
        if (mapper is null || match is null)
        {
            return null;
        }
        MoleculeMsReference? reference;
        try
        {
            reference = mapper.MoleculeMsRefer(match);
        }
        catch
        {
            reference = null;
        }
        return reference is null ? null : ToSpectrum(reference);
    }

    public static MsSpectrum ToSpectrum(MoleculeMsReference reference)
    {
        var peaks = (reference.Spectrum ?? new List<SpectrumPeak>())
            .Where(p => p.Intensity > 0)
            .Select(p => new SpectrumPeakPoint(p.Mass, p.Intensity))
            .OrderBy(p => p.Mz)
            .ToList();
        return new MsSpectrum($"Reference: {reference.Name} ({reference.AdductType?.AdductIonName})", reference.PrecursorMz, peaks);
    }

    /// <summary>Reads an MSP library (text .msp or serialized .msp2) with the upstream parser.</summary>
    public static Task<IReadOnlyList<MoleculeMsReference>> LoadMspLibraryAsync(string path, CancellationToken ct = default)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<MoleculeMsReference> list = LibraryHandler.ReadMspLibrary(path) ?? new List<MoleculeMsReference>();
            return list;
        }, ct);

    /// <summary>Finds the best library record for a peak by name (and precursor m/z when several share the name).</summary>
    public static MoleculeMsReference? FindReference(IReadOnlyList<MoleculeMsReference> library, string name, double precursorMz, double tolerance = 0.05)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        var candidates = library.Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }
        return candidates.OrderBy(r => Math.Abs(r.PrecursorMz - precursorMz)).First();
    }

    // ------------------------------------------------------------------ alignment

    public static Task<AlignmentTable> LoadAlignmentTableAsync(AlignmentFileBean alignmentFile, IReadOnlyList<AnalysisFileBean> files, CancellationToken ct = default)
        => LoadAlignmentTableAsync(alignmentFile, files, null, ct);

    /// <summary>
    /// Reads the alignment result into table rows. Pass <paramref name="loaded"/> to rebuild the rows
    /// from a container already in memory, which is what a hand edit needs: the edits live in that
    /// object and re-reading the files would throw them away.
    /// </summary>
    public static Task<AlignmentTable> LoadAlignmentTableAsync(AlignmentFileBean alignmentFile, IReadOnlyList<AnalysisFileBean> files, AlignmentResultContainer? loaded, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alignmentFile);
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var container = loaded ?? AlignmentResultContainer.Load(alignmentFile);
            if (container?.AlignmentSpotProperties is null)
            {
                var tsv = Path.Combine(Path.GetDirectoryName(alignmentFile.FilePath) ?? string.Empty, alignmentFile.FileName + ".mdalign");
                if (File.Exists(tsv))
                {
                    return LoadAlignmentTableFromTsv(tsv);
                }
                throw new FileNotFoundException("Alignment result not found.", alignmentFile.FilePath);
            }

            var samples = files
                .Select(f => new SampleInfo(f.AnalysisFileId, f.AnalysisFileName, f.AnalysisFileClass, AnalysisFileFactory.FromUpstream(f.AnalysisFileType).ToString()))
                .ToList();
            var byId = samples.ToDictionary(s => s.FileId);
            var spots = new List<AlignmentSpotRow>(container.AlignmentSpotProperties.Count);
            foreach (var spot in container.AlignmentSpotProperties)
            {
                var match = spot.MatchResults?.Representative;
                var aligned = spot.AlignedPeakProperties ?? new List<AlignmentChromPeakFeature>();
                var heights = aligned
                    .Select(p =>
                    {
                        var info = byId.TryGetValue(p.FileID, out var s) ? s : new SampleInfo(p.FileID, p.FileName, string.Empty, string.Empty);
                        return new SampleValue(p.FileID, info.FileName, info.Class, p.PeakHeightTop);
                    })
                    .ToList();
                var samplePeaks = aligned
                    .Select(p =>
                    {
                        var info = byId.TryGetValue(p.FileID, out var s) ? s : new SampleInfo(p.FileID, p.FileName, string.Empty, string.Empty);
                        return new AlignedSamplePeak(p.FileID, info.FileName, info.Class, info.SampleType,
                            p.ChromXsTop?.RT?.Value ?? double.NaN, p.ChromXsLeft?.RT?.Value ?? double.NaN, p.ChromXsRight?.RT?.Value ?? double.NaN,
                            p.Mass, p.PeakHeightTop, p.PeakAreaAboveZero, p.PeakShape?.SignalToNoise ?? 0, p.MasterPeakID < 0);
                    })
                    .ToList();
                spots.Add(new AlignmentSpotRow
                {
                    Spot = spot,
                    Id = spot.MasterAlignmentID,
                    Name = spot.Name ?? string.Empty,
                    Rt = spot.TimesCenter?.RT?.Value ?? 0,
                    Mz = spot.MassCenter,
                    AverageHeight = spot.HeightAverage,
                    FillPercent = spot.FillParcentage * 100.0,
                    Score = match is null || match.IsUnknown ? 0 : match.TotalScore,
                    Adduct = spot.AdductType?.AdductIonName ?? string.Empty,
                    Ontology = spot.Ontology ?? string.Empty,
                    Formula = spot.Formula?.FormulaString ?? string.Empty,
                    InChIKey = spot.InChIKey ?? string.Empty,
                    SignalToNoiseAverage = spot.SignalToNoiseAve,
                    SampleHeights = heights,
                    SamplePeaks = samplePeaks,
                    MatchResult = match is null || match.IsUnknown ? null : match,
                    MsmsAssigned = aligned.Any(p => p.IsMsmsAssigned),
                    RepresentativeFileId = spot.RepresentativeFileID,
                    IsotopicPeaks = (spot.IsotopicPeaks ?? new List<CompMs.Common.DataObj.Property.IsotopicPeak>())
                        .Where(p => p is not null)
                        .Select(p => new SpectrumPeakPoint(p.Mass, p.AbsoluteAbundance))
                        .ToList(),
                    Comment = spot.Comment ?? string.Empty,
                    MonoisotopicPercentage = spot.MonoIsotopicPercentage,
                    IsManuallyAnnotated = spot.MatchResults?.IsManuallyModifiedRepresentative ?? false,
                    Candidates = BuildCandidates(spot.MatchResults, match),
                });
            }
            return new AlignmentTable(samples, spots, "container", container);
        }, ct);
    }


    /// <summary>
    /// The library matches kept for a feature, best first. MS-DIAL stores several and reports one;
    /// a reviewer who disagrees needs to see the runners-up and what separated them.
    /// </summary>
    private static IReadOnlyList<AnnotationCandidate> BuildCandidates(MsScanMatchResultContainer? container, MsScanMatchResult? representative)
    {
        if (container is null) return Array.Empty<AnnotationCandidate>();
        var results = container.MatchResults?.Where(r => r is not null && !r.IsUnknown && !r.IsDecoy).ToList();
        if (results is null || results.Count == 0) return Array.Empty<AnnotationCandidate>();
        return results
            .OrderByDescending(r => r.TotalScore)
            .Select(r => new AnnotationCandidate(
                string.IsNullOrEmpty(r.Name) ? "(unnamed record)" : r.Name,
                r.TotalScore, r.SimpleDotProduct, r.WeightedDotProduct, r.ReverseDotProduct,
                r.MatchedPeaksCount, r.MatchedPeaksPercentage, r.AcurateMassSimilarity, r.RtSimilarity,
                ReferenceEquals(r, representative), r.IsSpectrumMatch,
                r.IsLipidClassMatch, r.IsLipidChainsMatch, r.IsLipidPositionMatch,
                r.Source.ToString(), r.LibraryID))
            .ToList();
    }

    /// <summary>Fallback parser for the exported .mdalign TSV (MS-DIAL alignment export format).</summary>
    public static AlignmentTable LoadAlignmentTableFromTsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var headerIndex = Array.FindIndex(lines, l => l.StartsWith("Alignment ID\t", StringComparison.Ordinal));
        if (headerIndex < 0)
        {
            throw new InvalidDataException("Not an MS-DIAL alignment export: header row 'Alignment ID' not found.");
        }
        var header = lines[headerIndex].Split('\t');
        int Col(string name) => Array.FindIndex(header, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var idCol = Col("Alignment ID");
        var rtCol = Col("Average Rt(min)");
        var mzCol = Col("Average Mz");
        var nameCol = Col("Metabolite name");
        var adductCol = Col("Adduct type");
        var fillCol = Col("Fill %");
        var scoreCol = Col("Total score");
        // sample columns follow the last known metadata column ("MS/MS spectrum")
        var lastMetaCol = Math.Max(Col("MS/MS spectrum"), Col("MS1 isotopic spectrum"));
        var sampleStart = lastMetaCol + 1;
        var sampleEnd = header.Length; // Average/Stdev blocks (if any) come after a blank column
        for (var c = sampleStart; c < header.Length; c++)
        {
            if (string.IsNullOrWhiteSpace(header[c])) { sampleEnd = c; break; }
        }

        var classRow = lines.Take(headerIndex).Select(l => l.Split('\t')).FirstOrDefault(cells => cells.Length > sampleStart && cells.Skip(1).Any(c => c.Trim() == "Class"));
        var typeRow = lines.Take(headerIndex).Select(l => l.Split('\t')).FirstOrDefault(cells => cells.Length > sampleStart && cells.Skip(1).Any(c => c.Trim() == "File type"));
        var samples = new List<SampleInfo>();
        for (var c = sampleStart; c < sampleEnd; c++)
        {
            var cls = classRow is not null && c < classRow.Length ? classRow[c] : string.Empty;
            var type = typeRow is not null && c < typeRow.Length ? typeRow[c] : string.Empty;
            samples.Add(new SampleInfo(c - sampleStart, header[c], cls, type));
        }

        double D(string[] cells, int col) => col >= 0 && col < cells.Length && double.TryParse(cells[col], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        string S(string[] cells, int col) => col >= 0 && col < cells.Length ? cells[col] : string.Empty;

        var spots = new List<AlignmentSpotRow>();
        foreach (var line in lines.Skip(headerIndex + 1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split('\t');
            var heights = new List<SampleValue>();
            for (var c = sampleStart; c < sampleEnd; c++)
            {
                var s = samples[c - sampleStart];
                heights.Add(new SampleValue(s.FileId, s.FileName, s.Class, D(cells, c)));
            }
            var samplePeaks = heights.Select(h => new AlignedSamplePeak(h.FileId, h.FileName, h.Class, samples[h.FileId].SampleType, D(cells, rtCol), double.NaN, double.NaN, D(cells, mzCol), h.Height, double.NaN, double.NaN, false)).ToList();
            spots.Add(new AlignmentSpotRow
            {
                SamplePeaks = samplePeaks,
                Id = (int)D(cells, idCol),
                Name = S(cells, nameCol),
                Rt = D(cells, rtCol),
                Mz = D(cells, mzCol),
                Adduct = S(cells, adductCol),
                FillPercent = D(cells, fillCol) * (D(cells, fillCol) <= 1.0 ? 100.0 : 1.0),
                Score = D(cells, scoreCol),
                AverageHeight = heights.Count == 0 ? 0 : heights.Average(h => h.Height),
                SampleHeights = heights,
            });
        }
        return new AlignmentTable(samples, spots, "tsv");
    }
}
