using System.Text.Json.Nodes;
using MilX.Pipeline.Chemistry;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>The measured isotope envelope of an ion against theory: M, M+1, M+2… from the averaged survey spectrum, the spacing checked.</summary>
internal static class Isotopes
{
    public const double NeutronSpacing = 1.0033548378;

    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var file = Common.OpenTarget(args, store, "isotopes", headersOnly: false, ct);
        var mz = args.OptionalDouble("mz") ?? throw new CliError(ErrorCode.BadArguments, "isotopes needs --mz");
        var charge = Math.Max(1, args.Int("charge", 1));
        var tol = args.Double("tol", 0.01);
        var count = Math.Clamp(args.Int("peaks", 4), 2, 12);
        var (source, indices) = Common.Source(args, file);
        var rt = args.Range("rt");
        MsSpectrum spectrum;
        if (rt is { } r)
        {
            spectrum = RawExplorer.AverageSpectrum(file.Raw, indices, r.Lo, r.Hi);
        }
        else
        {
            // no window: the scan where the ion is tallest
            var xic = RawExplorer.Xic(file.Raw, indices, mz, tol);
            var summary = PeakSummary.Of(xic.Points);
            if (!summary.Found) throw new CliError(ErrorCode.NothingFound, $"nothing at m/z {mz} ± {tol} in {source}");
            spectrum = RawExplorer.Spectrum(file.Raw, indices[summary.ApexIndex]);
            rt = (summary.ApexRt, summary.ApexRt);
        }
        var doc = new Document("isotopes", Common.Input(file, args));
        doc.Root["source"] = source.ToString();
        doc.Root["rt"] = new JsonArray(Num.Rt(rt!.Value.Lo), Num.Rt(rt.Value.Hi));
        doc.Root["charge"] = charge;
        var spacing = NeutronSpacing / charge;
        doc.Root["expected_spacing"] = Num.TheoryMz(spacing);

        IReadOnlyList<(double Mz, double Abundance)>? theory = null;
        if (args.Get("formula") is { } formula)
        {
            try
            {
                var counts = Formula.Parse(formula);
                theory = Formula.IsotopePattern(counts, null, minAbundance: 0.0005, maxPeaks: count);
                doc.Root["formula"] = Formula.Format(counts);
            }
            catch (FormulaException e) { throw new CliError(ErrorCode.BadArguments, "--formula: " + e.Message); }
        }

        var peaks = spectrum.Peaks;
        var envelope = new JsonArray();
        var rows = new List<JsonObject>();
        double? monoHeight = null;
        double? previous = null;
        for (var k = 0; k < count; k++)
        {
            var expectedMz = mz + k * spacing;
            var best = peaks.Where(p => Math.Abs(p.Mz - expectedMz) <= tol).OrderByDescending(p => p.Intensity).FirstOrDefault();
            var found = best.Intensity > 0;
            if (k == 0 && !found) throw new CliError(ErrorCode.NothingFound, $"no peak at m/z {mz} ± {tol} in the spectrum");
            if (k == 0) monoHeight = best.Intensity;
            var row = new JsonObject
            {
                ["isotope"] = k == 0 ? "M" : $"M+{k}",
                ["expected_mz"] = Num.TheoryMz(expectedMz),
                ["found"] = found,
                ["measured_mz"] = found ? Num.Mz(best.Mz) : null,
                ["intensity"] = found ? Num.Intensity(best.Intensity) : null,
                ["measured_relative"] = found && monoHeight > 0 ? Num.Ratio(best.Intensity / monoHeight.Value) : null,
                ["error_mda"] = found ? Math.Round((best.Mz - expectedMz) * 1000, 2) : null,
                ["spacing_from_previous"] = found && previous is { } prev ? Num.TheoryMz(best.Mz - prev) : null,
                ["spacing_error_mda"] = found && previous is { } prev2 ? Math.Round((best.Mz - prev2 - spacing) * 1000, 2) : null,
            };
            if (theory is not null)
            {
                var t = theory.FirstOrDefault(p => Math.Abs((p.Mz - theory[0].Mz) / charge - k * spacing) < spacing / 2);
                row["expected_relative"] = t.Abundance > 0 ? Num.Ratio(t.Abundance / theory[0].Abundance) : null;
                row["relative_error"] = t.Abundance > 0 && row["measured_relative"] is not null ? Num.Ratio(row["measured_relative"]!.GetValue<double>() - t.Abundance / theory[0].Abundance) : null;
            }
            if (found) previous = best.Mz;
            envelope.Add(row);
            rows.Add((JsonObject)row.DeepClone());
        }
        doc.Root["envelope"] = envelope;
        doc.Rows = rows;
        doc.Root["spacing_check"] = rows.Skip(1).Where(r => r["spacing_error_mda"] is not null).Select(r => Math.Abs(r["spacing_error_mda"]!.GetValue<double>())).DefaultIfEmpty(double.NaN).Max() is var worst && !double.IsNaN(worst)
            ? new JsonObject { ["worst_error_mda"] = worst, ["ok"] = worst <= tol * 1000 }
            : null;
        return doc;
    }
}
