using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;

namespace MilX.Cli;

/// <summary>
/// Writes a measurement as indexed mzML 1.1.0 with the precursor each MS2 scan actually isolated, so a
/// vendor file read natively here can travel to a machine that has no vendor library and still answer
/// the same questions: a round trip through <c>scans</c> gives the same parent_mz to the decimal. Arrays
/// are 64-bit, zlib-compressed, little-endian; times are written in seconds, as the reader converts
/// them back to minutes exactly.
/// </summary>
public static class MzmlWriter
{
    public static void Write(string path, RawMeasurement raw, string name)
    {
        var spectra = raw.SpectrumList;
        using var file = File.Create(path);
        using var buffer = new MemoryStream();
        var head = new StringBuilder();
        head.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        head.Append("<indexedmzML xmlns=\"http://psi.hupo.org/ms/mzml\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.2_idx.xsd\">\n");
        head.Append(CultureInfo.InvariantCulture, $"<mzML xmlns=\"http://psi.hupo.org/ms/mzml\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd\" id=\"{Escape(name)}\" version=\"1.1.0\">\n");
        head.Append("<cvList count=\"2\"><cv id=\"MS\" fullName=\"Proteomics Standards Initiative Mass Spectrometry Ontology\" version=\"4.1.0\" URI=\"https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo\"/><cv id=\"UO\" fullName=\"Unit Ontology\" version=\"09:04:2014\" URI=\"https://raw.githubusercontent.com/bio-ontology-research-group/unit-ontology/master/unit.obo\"/></cvList>\n");
        var anyMs2 = spectra.Any(s => s.MsLevel > 1);
        var anyCentroid = spectra.Any(s => s.SpectrumRepresentation == SpectrumRepresentation.Centroid);
        head.Append("<fileDescription><fileContent><cvParam cvRef=\"MS\" accession=\"MS:1000579\" name=\"MS1 spectrum\" value=\"\"/>");
        if (anyMs2) head.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000580\" name=\"MSn spectrum\" value=\"\"/>");
        head.Append(anyCentroid ? "<cvParam cvRef=\"MS\" accession=\"MS:1000127\" name=\"centroid spectrum\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000128\" name=\"profile spectrum\" value=\"\"/>");
        head.Append("</fileContent>");
        head.Append(CultureInfo.InvariantCulture, $"<sourceFileList count=\"1\"><sourceFile id=\"RAW1\" name=\"{Escape(Path.GetFileName(raw.SourceFileInfo?.Location ?? name))}\" location=\"file:///{Escape(Path.GetDirectoryName(raw.SourceFileInfo?.Location ?? string.Empty) ?? string.Empty)}\"/></sourceFileList></fileDescription>\n");
        head.Append(CultureInfo.InvariantCulture, $"<sampleList count=\"1\"><sample id=\"sample1\" name=\"{Escape(raw.Sample?.Name ?? name)}\"/></sampleList>\n");
        head.Append(CultureInfo.InvariantCulture, $"<softwareList count=\"1\"><software id=\"milx\" version=\"{Cli.Version}\"><cvParam cvRef=\"MS\" accession=\"MS:1000799\" name=\"custom unreleased software tool\" value=\"MIL-X milx convert\"/></software></softwareList>\n");
        head.Append(CultureInfo.InvariantCulture, $"<instrumentConfigurationList count=\"1\"><instrumentConfiguration id=\"IC1\"><cvParam cvRef=\"MS\" accession=\"MS:1000031\" name=\"instrument model\" value=\"{Escape(raw.Sample?.InstrumentType ?? string.Empty)}\"/></instrumentConfiguration></instrumentConfigurationList>\n");
        head.Append("<dataProcessingList count=\"1\"><dataProcessing id=\"dp1\"><processingMethod order=\"0\" softwareRef=\"milx\"><cvParam cvRef=\"MS\" accession=\"MS:1000544\" name=\"Conversion to mzML\" value=\"\"/></processingMethod></dataProcessing></dataProcessingList>\n");
        head.Append(CultureInfo.InvariantCulture, $"<run id=\"{Escape(name)}\" defaultInstrumentConfigurationRef=\"IC1\" defaultSourceFileRef=\"RAW1\">\n<spectrumList count=\"{spectra.Count}\" defaultDataProcessingRef=\"dp1\">\n");
        Append(buffer, head.ToString());

        var offsets = new List<(string Id, long Offset)>(spectra.Count);
        for (var i = 0; i < spectra.Count; i++)
        {
            var s = spectra[i];
            var id = $"scan={i + 1} experiment={s.ExperimentID} cycle={s.ScanNumber}";
            offsets.Add((id, buffer.Position));
            Append(buffer, SpectrumXml(i, id, s));
            buffer.WriteByte((byte)'\n');
        }
        Append(buffer, "</spectrumList>\n</run>\n</mzML>\n");
        var indexOffset = buffer.Position;
        var index = new StringBuilder("<indexList count=\"1\">\n<index name=\"spectrum\">\n");
        foreach (var (id, offset) in offsets) index.Append(CultureInfo.InvariantCulture, $"<offset idRef=\"{Escape(id)}\">{offset}</offset>\n");
        index.Append("</index>\n</indexList>\n");
        Append(buffer, index.ToString());
        Append(buffer, $"<indexListOffset>{indexOffset.ToString(CultureInfo.InvariantCulture)}</indexListOffset>\n<fileChecksum>");
        buffer.Position = 0;
        var sha1 = Convert.ToHexString(SHA1.HashData(buffer)).ToLowerInvariant();
        buffer.Position = buffer.Length;
        Append(buffer, sha1 + "</fileChecksum>\n</indexedmzML>\n");
        buffer.Position = 0;
        buffer.CopyTo(file);
    }

    private static string SpectrumXml(int index, string id, RawSpectrum s)
    {
        var peaks = s.Spectrum ?? Array.Empty<RawPeakElement>();
        var n = peaks.Length;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<spectrum index=\"{index}\" id=\"{Escape(id)}\" defaultArrayLength=\"{n}\">");
        sb.Append(s.MsLevel > 1 ? "<cvParam cvRef=\"MS\" accession=\"MS:1000580\" name=\"MSn spectrum\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000579\" name=\"MS1 spectrum\" value=\"\"/>");
        sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000511\" name=\"ms level\" value=\"{Math.Max(1, s.MsLevel)}\"/>");
        sb.Append(s.ScanPolarity == ScanPolarity.Negative ? "<cvParam cvRef=\"MS\" accession=\"MS:1000129\" name=\"negative scan\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000130\" name=\"positive scan\" value=\"\"/>");
        sb.Append(s.SpectrumRepresentation == SpectrumRepresentation.Profile ? "<cvParam cvRef=\"MS\" accession=\"MS:1000128\" name=\"profile spectrum\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000127\" name=\"centroid spectrum\" value=\"\"/>");
        if (n > 0)
        {
            var bp = 0;
            double tic = 0;
            for (var i = 0; i < n; i++) { tic += peaks[i].Intensity; if (peaks[i].Intensity > peaks[bp].Intensity) bp = i; }
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000504\" name=\"base peak m/z\" value=\"{R(peaks[bp].Mz)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000505\" name=\"base peak intensity\" value=\"{R(peaks[bp].Intensity)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000131\" unitName=\"number of detector counts\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000285\" name=\"total ion current\" value=\"{R(s.TotalIonCurrent > 0 ? s.TotalIonCurrent : tic)}\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000528\" name=\"lowest observed m/z\" value=\"{R(peaks[0].Mz)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000527\" name=\"highest observed m/z\" value=\"{R(peaks[n - 1].Mz)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
        }
        var seconds = s.ScanStartTimeUnit == Units.Second ? s.ScanStartTime : s.ScanStartTime * 60.0;
        sb.Append("<scanList count=\"1\"><cvParam cvRef=\"MS\" accession=\"MS:1000795\" name=\"no combination\" value=\"\"/><scan>");
        sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000016\" name=\"scan start time\" value=\"{R(seconds)}\" unitCvRef=\"UO\" unitAccession=\"UO:0000010\" unitName=\"second\"/>");
        if (s.ScanWindowUpperLimit > s.ScanWindowLowerLimit)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<scanWindowList count=\"1\"><scanWindow><cvParam cvRef=\"MS\" accession=\"MS:1000501\" name=\"scan window lower limit\" value=\"{R(s.ScanWindowLowerLimit)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/><cvParam cvRef=\"MS\" accession=\"MS:1000500\" name=\"scan window upper limit\" value=\"{R(s.ScanWindowUpperLimit)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/></scanWindow></scanWindowList>");
        }
        sb.Append("</scan></scanList>");
        var parent = XicSources.ParentMz(s);
        if (s.MsLevel > 1 && parent > 0 && s.Precursor is { } p)
        {
            var target = p.IsolationTargetMz > 0 ? p.IsolationTargetMz : parent;
            sb.Append("<precursorList count=\"1\"><precursor><isolationWindow>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000827\" name=\"isolation window target m/z\" value=\"{R(target)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000828\" name=\"isolation window lower offset\" value=\"{R(p.IsolationWindowLowerOffset)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000829\" name=\"isolation window upper offset\" value=\"{R(p.IsolationWindowUpperOffset)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append("</isolationWindow><selectedIonList count=\"1\"><selectedIon>");
            sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000744\" name=\"selected ion m/z\" value=\"{R(parent)}\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>");
            sb.Append("</selectedIon></selectedIonList><activation><cvParam cvRef=\"MS\" accession=\"MS:1000133\" name=\"collision-induced dissociation\" value=\"\"/>");
            var ce = s.CollisionEnergy != 0 ? s.CollisionEnergy : p.CollisionEnergy;
            if (ce != 0) sb.Append(CultureInfo.InvariantCulture, $"<cvParam cvRef=\"MS\" accession=\"MS:1000045\" name=\"collision energy\" value=\"{R(ce)}\" unitCvRef=\"UO\" unitAccession=\"UO:0000266\" unitName=\"electronvolt\"/>");
            sb.Append("</activation></precursor></precursorList>");
        }
        sb.Append("<binaryDataArrayList count=\"2\">");
        sb.Append(BinaryArray(peaks.Select(q => q.Mz), true));
        sb.Append(BinaryArray(peaks.Select(q => q.Intensity), false));
        sb.Append("</binaryDataArrayList></spectrum>");
        return sb.ToString();
    }

    private static string BinaryArray(IEnumerable<double> values, bool isMz)
    {
        using var raw = new MemoryStream();
        foreach (var v in values) raw.Write(BitConverter.GetBytes(v));
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }
        var b64 = Convert.ToBase64String(compressed.ToArray());
        var kind = isMz
            ? "<cvParam cvRef=\"MS\" accession=\"MS:1000514\" name=\"m/z array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>"
            : "<cvParam cvRef=\"MS\" accession=\"MS:1000515\" name=\"intensity array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000131\" unitName=\"number of detector counts\"/>";
        return $"<binaryDataArray encodedLength=\"{b64.Length}\"><cvParam cvRef=\"MS\" accession=\"MS:1000523\" name=\"64-bit float\" value=\"\"/><cvParam cvRef=\"MS\" accession=\"MS:1000574\" name=\"zlib compression\" value=\"\"/>{kind}<binary>{b64}</binary></binaryDataArray>";
    }

    private static void Append(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
