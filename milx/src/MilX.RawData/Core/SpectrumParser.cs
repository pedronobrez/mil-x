using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompMs.Common.DataObj;

namespace CompMs.RawDataHandler.Core;

/// <summary>
/// Spectrum post-processing helpers shared by every reader. The public surface and the
/// numerical behaviour (rounding of accumulated m/z to 5 decimals and of intensities to
/// integers, ordering, index assignment) intentionally match the closed RawDataHandler
/// package, because MsdialCore and the DIMS/IMMS/LC-IM-MS libraries call these methods.
/// </summary>
public sealed class SpectrumParser
{
    private SpectrumParser() { }

    /// <summary>
    /// Wraps a spectrum list into a <see cref="RawMeasurement"/>: spectra are sorted by
    /// (scan start time, drift time, MS level), re-indexed positionally, and the distinct
    /// collision energies of MSn spectra are collected.
    /// </summary>
    public static RawMeasurement GetRawMeasurementObj(string filepath, int fileid, IReadOnlyList<RawSpectrum>? spectrumList) {
        var name = Path.GetFileNameWithoutExtension(filepath);
        var measurement = new RawMeasurement {
            SourceFileInfo = new RawSourceFileInfo { Id = fileid.ToString(), Name = name, Location = filepath },
            Sample = new RawSample { Id = fileid.ToString(), Name = name },
        };
        if (spectrumList is null || spectrumList.Count == 0) {
            return measurement;
        }
        var sorted = spectrumList
            .OrderBy(s => s.ScanStartTime)
            .ThenBy(s => s.DriftTime)
            .ThenBy(s => s.MsLevel)
            .ToList();
        var ceTargets = new List<double>();
        for (var i = 0; i < sorted.Count; i++) {
            var s = sorted[i];
            s.Index = i;
            s.ScanNumber = i;
            s.OriginalIndex = i;
            if (s.MsLevel > 1) {
                var ce = Math.Round(s.CollisionEnergy, 2);
                if (!ceTargets.Contains(ce)) {
                    ceTargets.Add(ce);
                }
            }
        }
        measurement.SpectrumList = sorted;
        measurement.CollisionEnergyTargets = ceTargets;
        return measurement;
    }

    public static List<double> LoadCollisionEnergyTargets(IReadOnlyList<RawSpectrum>? spectrumList) {
        var list = new List<double>();
        if (spectrumList is null) {
            return list;
        }
        for (var i = 0; i < spectrumList.Count; i++) {
            var s = spectrumList[i];
            if (s.MsLevel > 1) {
                var ce = Math.Round(s.CollisionEnergy, 2);
                if (!list.Contains(ce)) {
                    list.Add(ce);
                }
            }
        }
        return list;
    }

    // ------------------------------------------------------------------
    // Summary statistics from an existing peak array
    // ------------------------------------------------------------------

    public static void setSpectrumProperties(RawSpectrum spectrum) {
        var peaks = spectrum.Spectrum ?? Array.Empty<RawPeakElement>();
        Summarize(peaks, out var bpInt, out var bpMz, out var tic, out var lo, out var hi, out var min);
        spectrum.DefaultArrayLength = peaks.Length;
        spectrum.BasePeakIntensity = bpInt;
        spectrum.BasePeakMz = bpMz;
        spectrum.TotalIonCurrent = tic;
        spectrum.LowestObservedMz = lo;
        spectrum.HighestObservedMz = hi;
        spectrum.MinIntensity = min;
    }

    private static void Summarize(RawPeakElement[] peaks, out double basePeakIntensity, out double basePeakMz, out double tic, out double lowest, out double highest, out double minIntensity) {
        basePeakIntensity = 0.0;
        basePeakMz = 0.0;
        tic = 0.0;
        lowest = double.MaxValue;
        highest = double.MinValue;
        minIntensity = double.MaxValue;
        for (var i = 0; i < peaks.Length; i++) {
            var mz = peaks[i].Mz;
            var intensity = peaks[i].Intensity;
            tic += intensity;
            if (intensity > basePeakIntensity) {
                basePeakIntensity = intensity;
                basePeakMz = mz;
            }
            if (lowest > mz) lowest = mz;
            if (highest < mz) highest = mz;
            if (minIntensity > intensity) minIntensity = intensity;
        }
    }

    // ------------------------------------------------------------------
    // Accumulated (binned) spectra
    // ------------------------------------------------------------------

    /// <summary>Bins keyed by (int)(m/z * 1000); values are [mz of base contribution, summed intensity, base intensity].</summary>
    public static void AddToMassBinDictionary(Dictionary<int, double[]> accumulatedMassBin, double mass, double intensity) {
        var key = (int)(mass * 1000.0);
        if (!accumulatedMassBin.TryGetValue(key, out var bin)) {
            accumulatedMassBin[key] = new[] { mass, intensity, intensity };
        }
        else {
            bin[1] += intensity;
            if (bin[2] < intensity) {
                bin[0] = mass;
                bin[2] = intensity;
            }
        }
    }

    public static void AddToMassBinDictionary(Dictionary<int, MassBin> accumulatedMassBin, MassBinPool pool, double mass, double intensity) {
        var key = (int)(mass * 1000.0);
        if (accumulatedMassBin.TryGetValue(key, out var bin)) {
            bin.Add(mass, intensity);
        }
        else {
            accumulatedMassBin.Add(key, pool.Get(mass, intensity));
        }
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, Dictionary<int, MassBin> accumulatedMassBin) {
        var array = new RawPeakElement[accumulatedMassBin.Count];
        var basePeakIntensity = 0.0;
        var basePeakMz = 0.0;
        var tic = 0.0;
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        var minIntensity = double.MaxValue;
        var i = 0;
        foreach (var kvp in accumulatedMassBin) {
            var mz = kvp.Value.Mz;
            var summed = kvp.Value.SummedIntensity;
            tic += summed;
            if (summed > basePeakIntensity) {
                basePeakIntensity = summed;
                basePeakMz = mz;
            }
            if (lowest > mz) lowest = mz;
            if (highest < mz) highest = mz;
            if (minIntensity > summed) minIntensity = summed;
            array[i++] = new RawPeakElement { Mz = Math.Round(mz, 5), Intensity = Math.Round(summed, 0) };
        }
        Array.Sort(array, (x, y) => x.Mz.CompareTo(y.Mz));
        spectrum.Spectrum = array;
        spectrum.DefaultArrayLength = array.Length;
        spectrum.BasePeakIntensity = basePeakIntensity;
        spectrum.BasePeakMz = basePeakMz;
        spectrum.TotalIonCurrent = tic;
        spectrum.LowestObservedMz = lowest;
        spectrum.HighestObservedMz = highest;
        spectrum.MinIntensity = minIntensity;
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, Dictionary<int, double[]> accumulatedMassBin, bool isSortMz = true) {
        var list = new List<RawPeakElement>(accumulatedMassBin.Count);
        var basePeakIntensity = 0.0;
        var basePeakMz = 0.0;
        var tic = 0.0;
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        var minIntensity = double.MaxValue;
        foreach (var kvp in accumulatedMassBin) {
            var mz = kvp.Value[0];
            var summed = kvp.Value[1];
            tic += summed;
            if (summed > basePeakIntensity) {
                basePeakIntensity = summed;
                basePeakMz = mz;
            }
            if (lowest > mz) lowest = mz;
            if (highest < mz) highest = mz;
            if (minIntensity > summed) minIntensity = summed;
            list.Add(new RawPeakElement { Mz = Math.Round(mz, 5), Intensity = Math.Round(summed, 0) });
        }
        if (isSortMz) {
            list = list.OrderBy(p => p.Mz).ToList();
        }
        spectrum.Spectrum = list.ToArray();
        spectrum.DefaultArrayLength = list.Count;
        spectrum.BasePeakIntensity = basePeakIntensity;
        spectrum.BasePeakMz = basePeakMz;
        spectrum.TotalIonCurrent = tic;
        spectrum.LowestObservedMz = lowest;
        spectrum.HighestObservedMz = highest;
        spectrum.MinIntensity = minIntensity;
    }

    /// <summary>
    /// Builds a spectrum from a dense accumulated intensity array indexed by (int)(m/z * 100000).
    /// </summary>
    public static void setSpectrumProperties(RawSpectrum spectrum, double[] accumulatedMassIntensityArray) {
        var list = new List<RawPeakElement>();
        for (var i = 0; i < accumulatedMassIntensityArray.Length; i++) {
            var intensity = accumulatedMassIntensityArray[i];
            if (intensity <= 0) continue;
            list.Add(new RawPeakElement { Mz = Math.Round(i / 100000.0, 5), Intensity = Math.Round(intensity, 0) });
        }
        spectrum.Spectrum = list.ToArray();
        setSpectrumProperties(spectrum);
    }

    // ------------------------------------------------------------------
    // Building spectra from parallel mass / intensity arrays (used by vendor plugins)
    // ------------------------------------------------------------------

    public static void setSpectrumProperties(RawSpectrum spectrum, double[] masses, double[] intensities, double peakCutOff, bool isSortMz = true) {
        FromArrays(spectrum, masses, intensities, peakCutOff, isSortMz, null, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, double[] masses, float[] intensities, double peakCutOff) {
        FromArrays(spectrum, masses, ToDouble(intensities), peakCutOff, true, null, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, float[] masses, float[] intensities, double peakCutOff) {
        FromArrays(spectrum, ToDouble(masses), ToDouble(intensities), peakCutOff, true, null, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, List<double> masses, List<double> intensities, double peakCutOff) {
        FromArrays(spectrum, masses.ToArray(), intensities.ToArray(), peakCutOff, true, null, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, double[] masses, double[] intensities, double peakCutOff, ref double[] accumulatedMassIntensityArray) {
        FromArrays(spectrum, masses, intensities, peakCutOff, true, accumulatedMassIntensityArray, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, List<double> masses, List<double> intensities, double peakCutOff, ref double[] accumulatedMassIntensityArray) {
        FromArrays(spectrum, masses.ToArray(), intensities.ToArray(), peakCutOff, true, accumulatedMassIntensityArray, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, float[] masses, float[] intensities, double peakCutOff, ref double[] accumulatedMassIntensityArray) {
        FromArrays(spectrum, ToDouble(masses), ToDouble(intensities), peakCutOff, true, accumulatedMassIntensityArray, null, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, double[] masses, double[] intensities, double peakCutOff, Dictionary<int, double[]> accumulatedMassBin, bool isSortMz = true) {
        FromArrays(spectrum, masses, intensities, peakCutOff, isSortMz, null, accumulatedMassBin, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, List<double> masses, List<double> intensities, double peakCutOff, Dictionary<int, double[]> accumulatedMassBin) {
        FromArrays(spectrum, masses.ToArray(), intensities.ToArray(), peakCutOff, true, null, accumulatedMassBin, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, float[] masses, float[] intensities, double peakCutOff, Dictionary<int, double[]> accumulatedMassBin) {
        FromArrays(spectrum, ToDouble(masses), ToDouble(intensities), peakCutOff, true, null, accumulatedMassBin, null);
    }

    public static void setSpectrumProperties(RawSpectrum spectrum, double[] masses, double[] intensities, double peakCutOff, Dictionary<int, MassBin> accumulatedMassBin, MassBinPool pool, bool isSortMz = true) {
        FromArrays(spectrum, masses, intensities, peakCutOff, isSortMz, null, null, (accumulatedMassBin, pool));
    }

    private static double[] ToDouble(float[] values) {
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++) result[i] = values[i];
        return result;
    }

    private static void FromArrays(RawSpectrum spectrum, double[] masses, double[] intensities, double peakCutOff, bool isSortMz,
        double[]? denseAccumulator, Dictionary<int, double[]>? binAccumulator, (Dictionary<int, MassBin> bins, MassBinPool pool)? pooledAccumulator) {
        var list = new List<RawPeakElement>();
        var basePeakIntensity = 0.0;
        var basePeakMz = 0.0;
        var tic = 0.0;
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        var minIntensity = double.MaxValue;
        if (masses != null && masses.Length > 0) {
            var n = Math.Min(masses.Length, intensities.Length);
            for (var i = 0; i < n; i++) {
                var mz = masses[i];
                var intensity = intensities[i];
                if (intensity <= peakCutOff) continue;
                tic += intensity;
                if (intensity > basePeakIntensity) {
                    basePeakIntensity = intensity;
                    basePeakMz = mz;
                }
                if (lowest > mz) lowest = mz;
                if (highest < mz) highest = mz;
                if (minIntensity > intensity) minIntensity = intensity;
                list.Add(new RawPeakElement { Mz = Math.Round(mz, 5), Intensity = Math.Round(intensity, 0) });
                if (spectrum.MsLevel == 1) {
                    if (denseAccumulator != null) {
                        var idx = (int)(mz * 100000.0);
                        if (idx >= 0 && idx < denseAccumulator.Length) denseAccumulator[idx] += intensity;
                    }
                    if (binAccumulator != null) AddToMassBinDictionary(binAccumulator, mz, intensity);
                    if (pooledAccumulator != null) AddToMassBinDictionary(pooledAccumulator.Value.bins, pooledAccumulator.Value.pool, mz, intensity);
                }
            }
            spectrum.Spectrum = isSortMz ? list.OrderBy(p => p.Mz).ToArray() : list.ToArray();
        }
        spectrum.DefaultArrayLength = list.Count;
        spectrum.BasePeakIntensity = basePeakIntensity;
        spectrum.BasePeakMz = basePeakMz;
        spectrum.TotalIonCurrent = tic;
        spectrum.LowestObservedMz = lowest;
        spectrum.HighestObservedMz = highest;
        spectrum.MinIntensity = minIntensity;
    }

    // ------------------------------------------------------------------
    // Ion-mobility frame accumulation (LC-IM-MS)
    // ------------------------------------------------------------------

    /// <summary>
    /// Accumulates all MS1 drift scans of each frame (spectra sharing a ScanNumber) into one
    /// summed MS1 spectrum per frame.
    /// </summary>
    public static List<RawSpectrum> GetAccumulatedMs1Spectrum(List<RawSpectrum> spectra) {
        var result = new List<RawSpectrum>();
        spectra = spectra.Where(s => s.MsLevel == 1).ToList();
        if (spectra.Count == 0) {
            return result;
        }
        var frames = GetFrameRanges(spectra);
        var pool = new MassBinPool();
        var bins = new Dictionary<int, MassBin>();
        for (var i = 0; i < frames.Count; i++) {
            bins.Clear();
            var (start, end) = frames[i];
            var lowest = spectra[start].LowestObservedMz;
            var highest = spectra[start].HighestObservedMz;
            for (var j = start; j <= end; j++) {
                var peaks = spectra[j].Spectrum;
                for (var k = 0; k < peaks.Length; k++) {
                    AddToMassBinDictionary(bins, pool, peaks[k].Mz, peaks[k].Intensity);
                }
                lowest = Math.Min(spectra[j].LowestObservedMz, lowest);
                highest = Math.Max(spectra[j].HighestObservedMz, highest);
            }
            var first = spectra[start];
            var accumulated = new RawSpectrum {
                OriginalIndex = first.OriginalIndex,
                Index = i,
                ScanNumber = first.ScanNumber,
                ScanStartTime = first.ScanStartTime,
                ScanStartTimeUnit = first.ScanStartTimeUnit,
                MsLevel = first.MsLevel,
                ScanPolarity = first.ScanPolarity,
                Precursor = first.Precursor,
                DriftScanNumber = first.DriftScanNumber,
                DriftTime = first.DriftTime,
                DriftTimeUnit = first.DriftTimeUnit,
                MaldiFrameInfo = first.MaldiFrameInfo,
                LowestObservedMz = lowest,
                HighestObservedMz = highest,
            };
            setSpectrumProperties(accumulated, bins);
            result.Add(accumulated);
            pool.BatchReturn(bins.Values);
        }
        return result;
    }

    /// <summary>Returns [start, end] index ranges of consecutive spectra sharing the same ScanNumber.</summary>
    public static List<(int Start, int End)> GetFrameRanges(List<RawSpectrum> spectra) {
        var list = new List<(int, int)>();
        if (spectra.Count == 0) {
            return list;
        }
        var start = 0;
        var end = 0;
        var scanNumber = spectra[0].ScanNumber;
        for (var i = 0; i < spectra.Count; i++) {
            if (spectra[i].ScanNumber == scanNumber) {
                end = i;
                continue;
            }
            scanNumber = spectra[i].ScanNumber;
            list.Add((start, end));
            start = i;
            end = i;
        }
        list.Add((start, end));
        return list;
    }
}
