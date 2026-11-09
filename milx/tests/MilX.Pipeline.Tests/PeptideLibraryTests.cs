using CompMs.Common.Parser;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// MIL-Q writes MSP libraries of peptide spectra — multiply charged precursors, b and y fragments.
/// MS-DIAL's MSP import has to keep the charge and the precursor type of a 2+ or 3+ record for the
/// annotation to search the right precursor; this holds it to that on a small peptide library.
/// </summary>
public class PeptideLibraryTests
{
    private const string Library = """
        NAME: LVNEVTEFAK/2
        PRECURSORMZ: 575.3111
        PRECURSORTYPE: [M+2H]2+
        FORMULA: C51H85N11O17
        IONMODE: Positive
        RETENTIONTIME: 22.4
        COLLISIONENERGY: 28
        COMMENT: MIL-Q peptide library; b/y
        Num Peaks: 6
        213.1598	18
        342.2024	35
        471.2450	22
        665.3617	100
        764.4301	60
        893.4727	40

        NAME: DLGEEHFKGLVLIAFSQYLQQCPFDEHVK/3
        PRECURSORMZ: 1137.2277
        PRECURSORTYPE: [M+3H]3+
        IONMODE: Positive
        Num Peaks: 3
        175.1190	30
        290.1459	100
        1042.5473	55

        NAME: Caffeine
        PRECURSORMZ: 195.0877
        PRECURSORTYPE: [M+H]+
        IONMODE: Positive
        Num Peaks: 2
        138.0662	100
        110.0713	35

        """;

    [Fact]
    public void Multiply_charged_records_keep_their_charge_and_precursor()
    {
        var path = Path.Combine(Path.GetTempPath(), "milx-peptides-" + Guid.NewGuid().ToString("N") + ".msp");
        File.WriteAllText(path, Library);
        try
        {
            var records = MspFileParser.MspFileReader(path);
            Assert.Equal(3, records.Count);

            var doubly = records[0];
            Assert.Equal("LVNEVTEFAK/2", doubly.Name);
            Assert.Equal("[M+2H]2+", doubly.AdductType.AdductIonName);
            Assert.Equal(2, doubly.AdductType.ChargeNumber);
            Assert.True(doubly.AdductType.FormatCheck);
            Assert.Equal(575.3111, doubly.PrecursorMz, 3);
            Assert.Equal(6, doubly.Spectrum.Count);
            Assert.Equal(665.3617, doubly.Spectrum.OrderByDescending(p => p.Intensity).First().Mass, 3);
            Assert.Equal(CompMs.Common.Enum.IonMode.Positive, doubly.IonMode);

            var triply = records[1];
            Assert.Equal(3, triply.AdductType.ChargeNumber);
            Assert.Equal("[M+3H]3+", triply.AdductType.AdductIonName);
            Assert.Equal(1137.2277, triply.PrecursorMz, 3);
            // the neutral mass a 3+ ion stands for: (m/z × 3) − 3 protons, within the rounding of the written precursor
            var neutral = triply.AdductType.ConvertToExactMass(triply.PrecursorMz);
            Assert.InRange(neutral, 3408.6, 3408.7);

            var singly = records[2];
            Assert.Equal(1, singly.AdductType.ChargeNumber);
            Assert.Equal(195.0877, singly.PrecursorMz, 3);
        }
        finally { File.Delete(path); }
    }
}
