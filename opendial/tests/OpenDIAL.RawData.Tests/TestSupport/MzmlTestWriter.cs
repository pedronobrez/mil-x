using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OpenDIAL.RawData.Tests.TestSupport;

public enum ArrayEncoding { Float64, Float32, NumpressLinear, NumpressPic, NumpressSlof }

internal sealed class TestSpectrum
{
    public int MsLevel { get; set; } = 1;
    public double RtSeconds { get; set; }
    public bool Positive { get; set; } = true;
    public double[] Mz { get; set; } = Array.Empty<double>();
    public double[] Intensity { get; set; } = Array.Empty<double>();
    public double? PrecursorMz { get; set; }
    public double? IsolationTarget { get; set; }
    public double IsolationLower { get; set; } = 0.5;
    public double IsolationUpper { get; set; } = 0.5;
    public double? CollisionEnergy { get; set; }
    public double? DriftTimeMs { get; set; }
    public double? InverseK0 { get; set; }
    public string? ParamGroupRef { get; set; }
    public bool RtInMinutes { get; set; }
    public bool OmitSelectedIon { get; set; }
}

/// <summary>Minimal mzML writer used to craft edge-case files for the reader tests.</summary>
internal sealed class MzmlTestWriter
{
    public bool Zlib { get; set; } = true;
    public ArrayEncoding MzEncoding { get; set; } = ArrayEncoding.Float64;
    public ArrayEncoding IntensityEncoding { get; set; } = ArrayEncoding.Float32;
    public bool Indexed { get; set; } = true;
    public Dictionary<string, string> ParamGroups { get; } = new();
    public bool WriteTic { get; set; } = true;
    public bool WriteChromatogram { get; set; }

    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    private static byte[] Compress(byte[] raw) {
        using var ms = new MemoryStream();
        // zlib header (CMF/FLG) + deflate + adler32 trailer
        ms.WriteByte(0x78);
        ms.WriteByte(0x9c);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true)) {
            deflate.Write(raw, 0, raw.Length);
        }
        uint a = 1, b = 0;
        foreach (var by in raw) { a = (a + by) % 65521; b = (b + a) % 65521; }
        var adler = (b << 16) | a;
        ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16)); ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private string BinaryArray(double[] values, ArrayEncoding encoding, bool isMz) {
        byte[] raw;
        var cv = new StringBuilder();
        switch (encoding) {
            case ArrayEncoding.Float64:
                raw = new byte[values.Length * 8];
                Buffer.BlockCopy(values, 0, raw, 0, raw.Length);
                cv.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000523\" name=\"64-bit float\" value=\"\"/>");
                cv.Append(Zlib ? "<cvParam cvRef=\"MS\" accession=\"MS:1000574\" name=\"zlib compression\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000576\" name=\"no compression\" value=\"\"/>");
                break;
            case ArrayEncoding.Float32: {
                var f = new float[values.Length];
                for (var i = 0; i < f.Length; i++) f[i] = (float)values[i];
                raw = new byte[f.Length * 4];
                Buffer.BlockCopy(f, 0, raw, 0, raw.Length);
                cv.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000521\" name=\"32-bit float\" value=\"\"/>");
                cv.Append(Zlib ? "<cvParam cvRef=\"MS\" accession=\"MS:1000574\" name=\"zlib compression\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000576\" name=\"no compression\" value=\"\"/>");
                break;
            }
            case ArrayEncoding.NumpressLinear:
                raw = NumpressEncoder.EncodeLinear(values, 100000.0);
                cv.Append(Zlib ? "<cvParam cvRef=\"MS\" accession=\"MS:1002746\" name=\"MS-Numpress linear prediction compression followed by zlib compression\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1002312\" name=\"MS-Numpress linear prediction compression\" value=\"\"/>");
                break;
            case ArrayEncoding.NumpressPic:
                raw = NumpressEncoder.EncodePic(values);
                cv.Append(Zlib ? "<cvParam cvRef=\"MS\" accession=\"MS:1002747\" name=\"MS-Numpress positive integer compression followed by zlib compression\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1002313\" name=\"MS-Numpress positive integer compression\" value=\"\"/>");
                break;
            case ArrayEncoding.NumpressSlof: {
                var max = values.Length == 0 ? 1.0 : values.Max();
                var fp = 65535.0 / Math.Log(max + 1.0);
                raw = NumpressEncoder.EncodeSlof(values, fp);
                cv.Append(Zlib ? "<cvParam cvRef=\"MS\" accession=\"MS:1002748\" name=\"MS-Numpress short logged float compression followed by zlib compression\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1002314\" name=\"MS-Numpress short logged float compression\" value=\"\"/>");
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(encoding));
        }
        if (Zlib) raw = Compress(raw);
        cv.Append(isMz
            ? "<cvParam cvRef=\"MS\" accession=\"MS:1000514\" name=\"m/z array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/>"
            : "<cvParam cvRef=\"MS\" accession=\"MS:1000515\" name=\"intensity array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000131\" unitName=\"number of detector counts\"/>");
        var b64 = Convert.ToBase64String(raw);
        return $"<binaryDataArray encodedLength=\"{b64.Length}\">{cv}<binary>{b64}</binary></binaryDataArray>";
    }

    public string Write(string path, IReadOnlyList<TestSpectrum> spectra) {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        if (Indexed) sb.Append("<indexedmzML xmlns=\"http://psi.hupo.org/ms/mzml\">\n");
        sb.Append("<mzML xmlns=\"http://psi.hupo.org/ms/mzml\" id=\"test\" version=\"1.1.0\">\n");
        sb.Append("<cvList count=\"2\"><cv id=\"MS\" fullName=\"MS\" URI=\"x\"/><cv id=\"UO\" fullName=\"UO\" URI=\"y\"/></cvList>\n");
        sb.Append("<fileDescription><fileContent><cvParam cvRef=\"MS\" accession=\"MS:1000579\" name=\"MS1 spectrum\" value=\"\"/></fileContent>");
        sb.Append("<sourceFileList count=\"1\"><sourceFile id=\"RAW1\" name=\"test.raw\" location=\"file:///\"/></sourceFileList></fileDescription>\n");
        if (ParamGroups.Count > 0) {
            sb.Append("<referenceableParamGroupList count=\"").Append(ParamGroups.Count).Append("\">");
            foreach (var kv in ParamGroups) sb.Append("<referenceableParamGroup id=\"").Append(kv.Key).Append("\">").Append(kv.Value).Append("</referenceableParamGroup>");
            sb.Append("</referenceableParamGroupList>\n");
        }
        sb.Append("<run id=\"run1\" startTimeStamp=\"2026-01-01T00:00:00Z\">\n");
        sb.Append("<spectrumList count=\"").Append(spectra.Count).Append("\">\n");
        var offsets = new List<(string id, long offset)>();
        for (var i = 0; i < spectra.Count; i++) {
            var s = spectra[i];
            var id = $"scan={i + 1}";
            offsets.Add((id, Encoding.UTF8.GetByteCount(sb.ToString())));
            sb.Append("<spectrum index=\"").Append(i).Append("\" id=\"").Append(id).Append("\" defaultArrayLength=\"").Append(s.Mz.Length).Append("\">");
            if (s.ParamGroupRef != null) sb.Append("<referenceableParamGroupRef ref=\"").Append(s.ParamGroupRef).Append("\"/>");
            sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000511\" name=\"ms level\" value=\"").Append(s.MsLevel).Append("\"/>");
            if (s.ParamGroupRef == null) sb.Append(s.Positive ? "<cvParam cvRef=\"MS\" accession=\"MS:1000130\" name=\"positive scan\" value=\"\"/>" : "<cvParam cvRef=\"MS\" accession=\"MS:1000129\" name=\"negative scan\" value=\"\"/>");
            sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000127\" name=\"centroid spectrum\" value=\"\"/>");
            if (WriteTic) sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000285\" name=\"total ion current\" value=\"").Append(s.Intensity.Sum().ToString("R", Ci)).Append("\"/>");
            sb.Append("<scanList count=\"1\"><scan>");
            if (s.RtInMinutes) sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000016\" name=\"scan start time\" value=\"").Append((s.RtSeconds / 60.0).ToString("R", Ci)).Append("\" unitCvRef=\"UO\" unitAccession=\"UO:0000031\" unitName=\"minute\"/>");
            else sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000016\" name=\"scan start time\" value=\"").Append(s.RtSeconds.ToString("R", Ci)).Append("\" unitCvRef=\"UO\" unitAccession=\"UO:0000010\" unitName=\"second\"/>");
            if (s.DriftTimeMs != null) sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1002476\" name=\"ion mobility drift time\" value=\"").Append(s.DriftTimeMs.Value.ToString("R", Ci)).Append("\" unitCvRef=\"UO\" unitAccession=\"UO:0000028\" unitName=\"millisecond\"/>");
            if (s.InverseK0 != null) sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1002815\" name=\"inverse reduced ion mobility\" value=\"").Append(s.InverseK0.Value.ToString("R", Ci)).Append("\" unitCvRef=\"MS\" unitAccession=\"MS:1002814\" unitName=\"volt-second per square centimeter\"/>");
            sb.Append("<scanWindowList count=\"1\"><scanWindow><cvParam cvRef=\"MS\" accession=\"MS:1000501\" name=\"scan window lower limit\" value=\"50\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/><cvParam cvRef=\"MS\" accession=\"MS:1000500\" name=\"scan window upper limit\" value=\"1500\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/></scanWindow></scanWindowList>");
            sb.Append("</scan></scanList>");
            if (s.MsLevel > 1 && (s.PrecursorMz != null || s.IsolationTarget != null)) {
                sb.Append("<precursorList count=\"1\"><precursor>");
                if (s.IsolationTarget != null) {
                    sb.Append("<isolationWindow><cvParam cvRef=\"MS\" accession=\"MS:1000827\" name=\"isolation window target m/z\" value=\"").Append(s.IsolationTarget.Value.ToString("R", Ci)).Append("\"/>");
                    sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000828\" name=\"isolation window lower offset\" value=\"").Append(s.IsolationLower.ToString("R", Ci)).Append("\"/>");
                    sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000829\" name=\"isolation window upper offset\" value=\"").Append(s.IsolationUpper.ToString("R", Ci)).Append("\"/></isolationWindow>");
                }
                if (s.PrecursorMz != null && !s.OmitSelectedIon) {
                    sb.Append("<selectedIonList count=\"1\"><selectedIon><cvParam cvRef=\"MS\" accession=\"MS:1000744\" name=\"selected ion m/z\" value=\"").Append(s.PrecursorMz.Value.ToString("R", Ci)).Append("\"/></selectedIon></selectedIonList>");
                }
                sb.Append("<activation><cvParam cvRef=\"MS\" accession=\"MS:1000422\" name=\"beam-type collision-induced dissociation\" value=\"\"/>");
                if (s.CollisionEnergy != null) sb.Append("<cvParam cvRef=\"MS\" accession=\"MS:1000045\" name=\"collision energy\" value=\"").Append(s.CollisionEnergy.Value.ToString("R", Ci)).Append("\" unitCvRef=\"UO\" unitAccession=\"UO:0000266\" unitName=\"electronvolt\"/>");
                sb.Append("</activation></precursor></precursorList>");
            }
            sb.Append("<binaryDataArrayList count=\"2\">");
            sb.Append(BinaryArray(s.Mz, MzEncoding, true));
            sb.Append(BinaryArray(s.Intensity, IntensityEncoding, false));
            sb.Append("</binaryDataArrayList></spectrum>\n");
        }
        sb.Append("</spectrumList>\n");
        if (WriteChromatogram) {
            var t = new double[] { 0, 30, 60 };
            var it = new double[] { 10, 20, 30 };
            sb.Append("<chromatogramList count=\"1\"><chromatogram index=\"0\" id=\"TIC\" defaultArrayLength=\"3\"><cvParam cvRef=\"MS\" accession=\"MS:1000235\" name=\"total ion current chromatogram\" value=\"\"/><binaryDataArrayList count=\"2\">");
            var timeArray = BinaryArray(t, ArrayEncoding.Float64, true).Replace("accession=\"MS:1000514\" name=\"m/z array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"", "accession=\"MS:1000595\" name=\"time array\" value=\"\" unitCvRef=\"UO\" unitAccession=\"UO:0000010\" unitName=\"second\"");
            sb.Append(timeArray).Append(BinaryArray(it, ArrayEncoding.Float32, false));
            sb.Append("</binaryDataArrayList></chromatogram></chromatogramList>\n");
        }
        sb.Append("</run>\n</mzML>\n");
        if (Indexed) {
            var indexOffset = Encoding.UTF8.GetByteCount(sb.ToString());
            sb.Append("<indexList count=\"1\"><index name=\"spectrum\">");
            foreach (var (id, off) in offsets) sb.Append("<offset idRef=\"").Append(id).Append("\">").Append(off).Append("</offset>");
            sb.Append("</index></indexList>\n<indexListOffset>").Append(indexOffset).Append("</indexListOffset>\n<fileChecksum>0000000000000000000000000000000000000000</fileChecksum>\n</indexedmzML>\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }
}
