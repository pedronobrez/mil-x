using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using OpenDIAL.Interop.OpenQuant;
using Xunit;

namespace OpenDIAL.Interop.OpenQuant.Tests;

public class OpenQuantInteropTests
{
    [Fact]
    public void Component_csv_has_openquant_header_and_precise_masses() {
        var c = new OpenQuantComponent { Name = "12,13-DiHOME", Group = "oxylipins", Precursor = 313.2384, Fragment = 183.1391, Rt = 14.7, RtHalfWidth = 0.6, Tolerance = 0.02, Unit = "Da", Adduct = "[M-H]-" };
        using var sw = new StringWriter();
        OpenQuantComponentCsv.Write(sw, new[] { c });
        var lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("name,group,precursor,fragment,rt,window,tolerance,unit,formula,adduct,is,internal_standard,response,concentration_unit,qualifier_of,ion_ratio,ion_ratio_tolerance,regression,weighting,lm_id", lines[0].TrimEnd('\r'));
        Assert.Equal("\"12,13-DiHOME\",oxylipins,313.2384,183.1391,14.7,0.6,0.02,Da,,[M-H]-,,,area,,,,,,,", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public void Alignment_spots_become_components_with_fragment_from_msms() {
        var spot = new AlignmentSpotProperty {
            Name = "Caffeine",
            MassCenter = 195.0877,
            TimesCenter = new ChromXs(1.2, ChromXType.RT, ChromXUnit.Min),
            AdductType = AdductIon.GetAdductIon("[M+H]+"),
        };
        var unknown = new AlignmentSpotProperty { Name = "Unknown", MassCenter = 300.1, TimesCenter = new ChromXs(2.5, ChromXType.RT, ChromXUnit.Min) };
        var dec = new MSDecResult { Spectrum = new List<SpectrumPeak> { new SpectrumPeak(138.0662, 100), new SpectrumPeak(195.0877, 20), new SpectrumPeak(110.0713, 35) } };
        var comps = OpenQuantComponentCsv.FromAlignmentSpots(new[] { spot, unknown }, new[] { dec, new MSDecResult() });
        Assert.Equal(2, comps.Count);
        Assert.Equal("Caffeine", comps[0].Name);
        Assert.Equal(195.0877, comps[0].Precursor, 4);
        Assert.Equal(138.0662, comps[0].Fragment!.Value, 4); // most intense product ion, not the residual precursor
        Assert.Equal(1.2, comps[0].Rt!.Value, 6);
        Assert.Equal("[M+H]+", comps[0].Adduct);
        Assert.StartsWith("m/z 300.1000 @ 2.50 min", comps[1].Name);
        Assert.Equal("unknown", comps[1].Group);
        Assert.Null(comps[1].Fragment);
        var annotatedOnly = OpenQuantComponentCsv.FromAlignmentSpots(new[] { spot, unknown }, null, new OpenQuantExportOptions { AnnotatedOnly = true });
        Assert.Single(annotatedOnly);
    }

    [Fact]
    public void Reads_an_oqproj_batch() {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".oqproj");
        File.WriteAllText(path, """
        {
          "version": 3,
          "method": {"components": [{"name": "PC 34:1", "group": "PC", "precursor": 760.5851, "fragment": 184.0733, "rt": 11.42, "rt_halfwidth": 0.6, "tolerance": 0.02, "unit": "Da", "is_internal_standard": false, "internal_standard": "PC 34:1 (d7)", "response": "ratio"}]},
          "samples": [
            {"path": "/data/QC01.wiff", "sample_index": 0, "name": "QC01", "sample_type": "Quality Control", "sample_group": "quality control", "actual_concentration": 75, "dilution_factor": 1.0, "comment": ""},
            {"path": "/data/S01.wiff", "sample_index": 1, "name": "S01", "sample_type": "Unknown", "sample_group": "plasma", "actual_concentration": null, "dilution_factor": 2.0, "comment": "hemolysed"},
            {"path": "/data/B.wiff", "sample_index": 0, "name": "B", "sample_type": "Blank", "sample_group": "", "actual_concentration": null, "dilution_factor": 1.0, "comment": ""}
          ],
          "results": [], "calibrations": {}
        }
        """);
        try {
            var p = OpenQuantProject.Load(path);
            Assert.Equal(3, p.Version);
            Assert.Equal(3, p.Samples.Count);
            Assert.Equal(AnalysisFileType.QC, p.Samples[0].ToAnalysisFileType());
            Assert.Equal("quality control", p.Samples[0].ToAnalysisClass());
            Assert.Equal(75.0, p.Samples[0].ActualConcentration);
            Assert.Equal(AnalysisFileType.Sample, p.Samples[1].ToAnalysisFileType());
            Assert.Equal(1, p.Samples[1].SampleIndex);
            Assert.Equal(2.0, p.Samples[1].DilutionFactor);
            Assert.Equal(AnalysisFileType.Blank, p.Samples[2].ToAnalysisFileType());
            Assert.Equal("Blank", p.Samples[2].ToAnalysisClass());
            Assert.Single(p.Components);
            Assert.Equal(184.0733, p.Components[0].Fragment!.Value, 4);
            Assert.Equal("ratio", p.Components[0].Response);
            Assert.True(OpenQuantProject.IsProject(path));
        }
        finally {
            File.Delete(path);
        }
    }
}
