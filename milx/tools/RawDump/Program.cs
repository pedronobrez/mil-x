using System.Globalization;
using System.Text;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;

// Usage: RawDump <file> [--full] [--max N]
//   Prints one line per spectrum with the fields MS-DIAL's algorithms consume.
//   --full also prints every peak (m/z, intensity) so two readers can be diffed exactly.
if (args.Length == 0) {
    Console.Error.WriteLine("usage: RawDump <rawfile> [--full] [--max N]");
    return 2;
}
var file = args[0];
var full = args.Contains("--full");
var max = int.MaxValue;
for (var i = 0; i < args.Length - 1; i++) {
    if (args[i] == "--max") max = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
}

var sw = System.Diagnostics.Stopwatch.StartNew();
using var access = new RawDataAccess(file, 0, false, false, false);
var m = access.GetMeasurement();
sw.Stop();
if (m is null) {
    Console.WriteLine("MEASUREMENT: null");
    return 1;
}
var ci = CultureInfo.InvariantCulture;
var sb = new StringBuilder();
sb.Append("MEASUREMENT\tspectra=").Append(m.SpectrumList?.Count ?? 0)
  .Append("\taccumulated=").Append(m.AccumulatedSpectrumList?.Count ?? 0)
  .Append("\tchromatograms=").Append(m.ChromatogramList?.Count ?? 0)
  .Append("\tce_targets=").Append(string.Join(",", (m.CollisionEnergyTargets ?? new List<double>()).Select(c => c.ToString("0.##", ci))))
  .Append("\tsource=").Append(m.SourceFileInfo?.Name).Append("\tsample=").Append(m.Sample?.Name)
  .Append("\tmethod=").Append(m.Method);
Console.WriteLine(sb.ToString());
Console.Error.WriteLine($"read time: {sw.Elapsed.TotalSeconds:0.00}s");
var n = 0;
foreach (var s in m.SpectrumList ?? new List<RawSpectrum>()) {
    if (n++ >= max) break;
    sb.Clear();
    sb.Append("S\t").Append(s.Index).Append('\t').Append(s.ScanNumber).Append('\t').Append(s.OriginalIndex).Append('\t').Append(s.DriftScanNumber)
      .Append("\tms").Append(s.MsLevel)
      .Append('\t').Append(s.ScanPolarity).Append('\t').Append(s.SpectrumRepresentation)
      .Append("\trt=").Append(s.ScanStartTime.ToString("R", ci)).Append(s.ScanStartTimeUnit)
      .Append("\tdt=").Append(s.DriftTime.ToString("R", ci)).Append(s.DriftTimeUnit)
      .Append("\tn=").Append(s.DefaultArrayLength).Append('/').Append(s.Spectrum?.Length ?? -1)
      .Append("\tlo=").Append(s.LowestObservedMz.ToString("R", ci)).Append("\thi=").Append(s.HighestObservedMz.ToString("R", ci))
      .Append("\tbpmz=").Append(s.BasePeakMz.ToString("R", ci)).Append("\tbpi=").Append(s.BasePeakIntensity.ToString("R", ci))
      .Append("\tmin=").Append(s.MinIntensity.ToString("R", ci)).Append("\ttic=").Append(s.TotalIonCurrent.ToString("R", ci))
      .Append("\twin=").Append(s.ScanWindowLowerLimit.ToString("R", ci)).Append('-').Append(s.ScanWindowUpperLimit.ToString("R", ci))
      .Append("\tce=").Append(s.CollisionEnergy.ToString("R", ci)).Append("\texp=").Append(s.ExperimentID);
    if (s.Precursor is { } p) {
        sb.Append("\tpre=").Append(p.SelectedIonMz.ToString("R", ci)).Append('|').Append(p.IsolationTargetMz.ToString("R", ci))
          .Append('|').Append(p.IsolationWindowLowerOffset.ToString("R", ci)).Append('|').Append(p.IsolationWindowUpperOffset.ToString("R", ci))
          .Append('|').Append(p.Dissociationmethod).Append('|').Append(p.CollisionEnergy.ToString("R", ci)).Append(p.CollisionEnergyUnit)
          .Append('|').Append(p.TimeBegin.ToString("R", ci)).Append('-').Append(p.TimeEnd.ToString("R", ci));
    }
    else sb.Append("\tpre=-");
    if (s.Product is { } pr) {
        sb.Append("\tprod=").Append(pr.IsolationTargetMz.ToString("R", ci)).Append('|').Append(pr.IsolationWindowLowerOffset.ToString("R", ci)).Append('|').Append(pr.IsolationWindowUpperOffset.ToString("R", ci));
    }
    Console.WriteLine(sb.ToString());
    if (full && s.Spectrum is { } peaks) {
        foreach (var pk in peaks) {
            Console.WriteLine($"P\t{pk.Mz.ToString("R", ci)}\t{pk.Intensity.ToString("R", ci)}");
        }
    }
}
foreach (var c in m.ChromatogramList ?? new List<RawChromatogram>()) {
    Console.WriteLine($"C\t{c.Index}\t{c.Id}\tn={c.DefaultArrayLength}/{c.Chromatogram?.Length ?? -1}\tsrm={c.IsSRM}");
    if (full && c.Chromatogram is { } pts) {
        foreach (var pt in pts) Console.WriteLine($"CP\t{pt.RtInMin.ToString("R", ci)}\t{pt.Intensity.ToString("R", ci)}");
    }
}
return 0;
