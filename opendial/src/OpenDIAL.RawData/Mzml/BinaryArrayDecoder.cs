using System;
using System.IO;
using System.IO.Compression;

namespace OpenDIAL.RawData.Mzml;

public enum BinaryArrayKind
{
    Unknown,
    Mz,
    Intensity,
    Time,
    Charge,
    SignalToNoise,
    Wavelength,
    IonMobility,
    NonStandard,
}

public enum BinaryPrecision
{
    Unknown,
    Float32,
    Float64,
    Int32,
    Int64,
}

public enum BinaryCompression
{
    None,
    Zlib,
    NumpressLinear,
    NumpressPic,
    NumpressSlof,
    NumpressLinearZlib,
    NumpressPicZlib,
    NumpressSlofZlib,
}

public enum BinaryUnit
{
    Unknown,
    Second,
    Minute,
    Millisecond,
    Mz,
    Counts,
    Nanometer,
    Dimensionless,
    OneOverK0,
}

/// <summary>
/// Description of one mzML &lt;binaryDataArray&gt; and the decoding of its base64 payload.
/// </summary>
public sealed class BinaryArrayDescriptor
{
    public BinaryArrayKind Kind { get; set; } = BinaryArrayKind.Unknown;
    public BinaryPrecision Precision { get; set; } = BinaryPrecision.Unknown;
    public BinaryCompression Compression { get; set; } = BinaryCompression.None;
    public BinaryUnit Unit { get; set; } = BinaryUnit.Unknown;
    public int EncodedLength { get; set; }
    public int? ArrayLength { get; set; }

    /// <summary>Applies a PSI-MS / UO accession to this descriptor. Returns true if it was understood.</summary>
    public bool ApplyAccession(string accession) {
        switch (accession) {
            // array kinds
            case "MS:1000514": Kind = BinaryArrayKind.Mz; return true;
            case "MS:1000515": Kind = BinaryArrayKind.Intensity; return true;
            case "MS:1000595": Kind = BinaryArrayKind.Time; return true;
            case "MS:1000516": Kind = BinaryArrayKind.Charge; return true;
            case "MS:1000517": Kind = BinaryArrayKind.SignalToNoise; return true;
            case "MS:1000617": Kind = BinaryArrayKind.Wavelength; return true;
            case "MS:1000786": Kind = BinaryArrayKind.NonStandard; return true;
            case "MS:1002816": // mean ion mobility array
            case "MS:1002893": // mean ion mobility drift time array
            case "MS:1003006": // mean inverse reduced ion mobility array
            case "MS:1003007": // raw inverse reduced ion mobility array
            case "MS:1003153": // raw ion mobility array
                Kind = BinaryArrayKind.IonMobility; return true;
            // precision
            case "MS:1000521": Precision = BinaryPrecision.Float32; return true;
            case "MS:1000523": Precision = BinaryPrecision.Float64; return true;
            case "MS:1000519": Precision = BinaryPrecision.Int32; return true;
            case "MS:1000522": Precision = BinaryPrecision.Int64; return true;
            // compression
            case "MS:1000576": Compression = BinaryCompression.None; return true;
            case "MS:1000574": Compression = BinaryCompression.Zlib; return true;
            case "MS:1002312": Compression = BinaryCompression.NumpressLinear; return true;
            case "MS:1002313": Compression = BinaryCompression.NumpressPic; return true;
            case "MS:1002314": Compression = BinaryCompression.NumpressSlof; return true;
            case "MS:1002746": Compression = BinaryCompression.NumpressLinearZlib; return true;
            case "MS:1002747": Compression = BinaryCompression.NumpressPicZlib; return true;
            case "MS:1002748": Compression = BinaryCompression.NumpressSlofZlib; return true;
            default: return false;
        }
    }

    public bool ApplyUnitAccession(string unitAccession) {
        switch (unitAccession) {
            case "UO:0000010": Unit = BinaryUnit.Second; return true;
            case "UO:0000031": Unit = BinaryUnit.Minute; return true;
            case "UO:0000028": Unit = BinaryUnit.Millisecond; return true;
            case "UO:0000018": Unit = BinaryUnit.Nanometer; return true;
            case "UO:0000186": Unit = BinaryUnit.Dimensionless; return true;
            case "MS:1000040": Unit = BinaryUnit.Mz; return true;
            case "MS:1000131": Unit = BinaryUnit.Counts; return true;
            case "MS:1002814": Unit = BinaryUnit.OneOverK0; return true;
            default: return false;
        }
    }

    /// <summary>Decodes the base64 text of a &lt;binary&gt; element into doubles.</summary>
    public double[] Decode(string base64) {
        if (string.IsNullOrWhiteSpace(base64)) {
            return Array.Empty<double>();
        }
        byte[] bytes;
        try {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException) {
            // some writers wrap lines or include stray whitespace that Convert does not tolerate
            bytes = Convert.FromBase64String(base64.Replace("\n", string.Empty).Replace("\r", string.Empty).Replace("\t", string.Empty).Replace(" ", string.Empty));
        }
        return DecodeBytes(bytes);
    }

    public double[] DecodeBytes(byte[] bytes) {
        if (!BitConverter.IsLittleEndian) {
            throw new PlatformNotSupportedException("mzML binary arrays are little-endian; big-endian hosts are not supported.");
        }
        switch (Compression) {
            case BinaryCompression.Zlib:
                bytes = Inflate(bytes);
                break;
            case BinaryCompression.NumpressLinearZlib:
                return Numpress.DecodeLinear(Inflate(bytes));
            case BinaryCompression.NumpressPicZlib:
                return Numpress.DecodePic(Inflate(bytes));
            case BinaryCompression.NumpressSlofZlib:
                return Numpress.DecodeSlof(Inflate(bytes));
            case BinaryCompression.NumpressLinear:
                return Numpress.DecodeLinear(bytes);
            case BinaryCompression.NumpressPic:
                return Numpress.DecodePic(bytes);
            case BinaryCompression.NumpressSlof:
                return Numpress.DecodeSlof(bytes);
        }

        var precision = Precision == BinaryPrecision.Unknown ? BinaryPrecision.Float32 : Precision;
        switch (precision) {
            case BinaryPrecision.Float64: {
                var n = bytes.Length / 8;
                var result = new double[n];
                Buffer.BlockCopy(bytes, 0, result, 0, n * 8);
                return result;
            }
            case BinaryPrecision.Float32: {
                var n = bytes.Length / 4;
                var tmp = new float[n];
                Buffer.BlockCopy(bytes, 0, tmp, 0, n * 4);
                var result = new double[n];
                for (var i = 0; i < n; i++) {
                    result[i] = tmp[i];
                }
                return result;
            }
            case BinaryPrecision.Int32: {
                var n = bytes.Length / 4;
                var tmp = new int[n];
                Buffer.BlockCopy(bytes, 0, tmp, 0, n * 4);
                var result = new double[n];
                for (var i = 0; i < n; i++) {
                    result[i] = tmp[i];
                }
                return result;
            }
            case BinaryPrecision.Int64: {
                var n = bytes.Length / 8;
                var tmp = new long[n];
                Buffer.BlockCopy(bytes, 0, tmp, 0, n * 8);
                var result = new double[n];
                for (var i = 0; i < n; i++) {
                    result[i] = tmp[i];
                }
                return result;
            }
            default:
                throw new NotSupportedException("Unknown binary precision");
        }
    }

    /// <summary>Inflates a zlib stream (RFC 1950: 2-byte header, deflate body, adler32 trailer).</summary>
    public static byte[] Inflate(byte[] zlibBytes) {
        if (zlibBytes.Length < 2) {
            return Array.Empty<byte>();
        }
        // Validate the zlib header so that a raw-deflate payload is still handled.
        var cmf = zlibBytes[0];
        var flg = zlibBytes[1];
        var isZlibHeader = (cmf & 0x0f) == 8 && ((cmf << 8) | flg) % 31 == 0;
        var offset = isZlibHeader ? 2 : 0;
        using var input = new MemoryStream(zlibBytes, offset, zlibBytes.Length - offset);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(Math.Max(64, zlibBytes.Length * 3));
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0) {
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
