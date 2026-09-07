using System;
using System.Collections.Generic;

namespace OpenDIAL.RawData.Mzml;

/// <summary>
/// Decoders for the MS-Numpress compression schemes (PSI-MS accessions MS:1002312 linear,
/// MS:1002313 pic, MS:1002314 slof). Implemented from the public specification
/// (Teleman et al., MCP 2014) and the reference algorithm description.
/// </summary>
public static class Numpress
{
    private static double DecodeFixedPoint(byte[] data) {
        // The fixed point is stored as a big-endian IEEE-754 double in the first 8 bytes.
        var fp = new byte[8];
        for (var i = 0; i < 8; i++) {
            fp[i] = data[7 - i];
        }
        if (!BitConverter.IsLittleEndian) {
            Array.Reverse(fp);
        }
        return BitConverter.ToDouble(fp, 0);
    }

    /// <summary>Reads one variable-length integer made of half bytes (nibbles).</summary>
    private static int DecodeInt(byte[] data, ref int pos, ref int half) {
        int head;
        if (half == 0) {
            head = data[pos] >> 4;
        }
        else {
            head = data[pos] & 0xf;
            pos++;
        }
        half = 1 - half;
        var res = 0;
        int n;
        if (head <= 8) {
            n = head;
        }
        else {
            // leading 0xf nibbles
            n = head - 8;
            var mask = 0xf0000000u;
            for (var i = 0; i < n; i++) {
                var m = mask >> (4 * i);
                res |= unchecked((int)m);
            }
        }
        if (n == 8) {
            return res;
        }
        for (var i = n; i < 8; i++) {
            int hb;
            if (half == 0) {
                hb = data[pos] >> 4;
            }
            else {
                hb = data[pos] & 0xf;
                pos++;
            }
            res |= hb << ((i - n) * 4);
            half = 1 - half;
        }
        return res;
    }

    /// <summary>Decodes an MS:1002312 "numpress linear" byte array (typically m/z values).</summary>
    public static double[] DecodeLinear(byte[] data) {
        if (data.Length < 8) {
            throw new FormatException("numpress linear: data too short for fixed point");
        }
        var fixedPoint = DecodeFixedPoint(data);
        if (data.Length == 8) {
            return Array.Empty<double>();
        }
        if (data.Length < 12) {
            throw new FormatException("numpress linear: corrupt input (first value)");
        }
        var result = new List<double>(Math.Max(16, data.Length / 2));
        var ints = new int[3];
        ints[1] = 0;
        for (var i = 0; i < 4; i++) {
            ints[1] |= (0xff & data[8 + i]) << (i * 8);
        }
        result.Add(ints[1] / fixedPoint);
        if (data.Length == 12) {
            return result.ToArray();
        }
        if (data.Length < 16) {
            throw new FormatException("numpress linear: corrupt input (second value)");
        }
        ints[2] = 0;
        for (var i = 0; i < 4; i++) {
            ints[2] |= (0xff & data[12 + i]) << (i * 8);
        }
        result.Add(ints[2] / fixedPoint);

        var half = 0;
        var pos = 16;
        while (pos < data.Length) {
            if (pos == data.Length - 1 && half == 1) {
                // trailing padding nibble
                if ((data[pos] & 0xf) != 0x8) {
                    throw new FormatException("numpress linear: corrupt trailing nibble");
                }
                break;
            }
            ints[0] = ints[1];
            ints[1] = ints[2];
            ints[2] = DecodeInt(data, ref pos, ref half);
            var extrapol = ints[1] + (ints[1] - ints[0]);
            var y = extrapol + ints[2];
            result.Add(y / fixedPoint);
            ints[2] = y;
        }
        return result.ToArray();
    }

    /// <summary>Decodes an MS:1002313 "numpress positive integer compression" byte array (intensities).</summary>
    public static double[] DecodePic(byte[] data) {
        var result = new List<double>(Math.Max(16, data.Length / 2));
        var half = 0;
        var pos = 0;
        while (pos < data.Length) {
            if (pos == data.Length - 1 && half == 1) {
                if ((data[pos] & 0xf) != 0x8) {
                    throw new FormatException("numpress pic: corrupt trailing nibble");
                }
                break;
            }
            var count = DecodeInt(data, ref pos, ref half);
            result.Add(count);
        }
        return result.ToArray();
    }

    /// <summary>Decodes an MS:1002314 "numpress short logged float compression" byte array (intensities).</summary>
    public static double[] DecodeSlof(byte[] data) {
        if (data.Length < 8) {
            throw new FormatException("numpress slof: data too short for fixed point");
        }
        var fixedPoint = DecodeFixedPoint(data);
        var n = (data.Length - 8) / 2;
        var result = new double[n];
        for (var i = 0; i < n; i++) {
            var x = (ushort)(data[8 + 2 * i] | (data[9 + 2 * i] << 8));
            result[i] = Math.Exp(x / fixedPoint) - 1.0;
        }
        return result;
    }
}
