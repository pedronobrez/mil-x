using System.Text;

namespace MilX.RawData.Tests.TestSupport;

/// <summary>
/// Test-only MS-Numpress encoders (linear, pic, slof) written from the published
/// specification, used to produce round-trip vectors for the production decoders.
/// </summary>
internal static class NumpressEncoder
{
    private static void EncodeFixedPoint(double fixedPoint, List<byte> result) {
        var bytes = BitConverter.GetBytes(fixedPoint);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
        for (var i = 0; i < 8; i++) result.Add(bytes[7 - i]);
    }

    private static void EncodeInt(int x, List<byte> halfBytes) {
        const uint mask = 0xf0000000;
        var ux = unchecked((uint)x);
        var init = ux & mask;
        if (init == 0) {
            var l = 8;
            for (var i = 1; i < 8; i++) {
                var m = mask >> (4 * i);
                if ((ux & m) != 0) { l = i; break; }
            }
            halfBytes.Add((byte)l);
            for (var i = l; i < 8; i++) halfBytes.Add((byte)((ux >> (4 * (i - l))) & 0xf));
        }
        else if (init == mask) {
            var l = 7;
            for (var i = 1; i < 8; i++) {
                var m = mask >> (4 * i);
                if ((ux & m) != m) { l = i; break; }
            }
            halfBytes.Add((byte)(l + 8));
            for (var i = l; i < 8; i++) halfBytes.Add((byte)((ux >> (4 * (i - l))) & 0xf));
        }
        else {
            halfBytes.Add(0);
            for (var i = 0; i < 8; i++) halfBytes.Add((byte)((ux >> (4 * i)) & 0xf));
        }
    }

    private static void PackHalfBytes(List<byte> halfBytes, List<byte> result) {
        var i = 0;
        for (; i + 1 < halfBytes.Count; i += 2) {
            result.Add((byte)((halfBytes[i] << 4) | (halfBytes[i + 1] & 0xf)));
        }
        if (i < halfBytes.Count) {
            result.Add((byte)((halfBytes[i] << 4) | 0x8));
        }
    }

    public static byte[] EncodeLinear(double[] data, double fixedPoint) {
        var result = new List<byte>();
        EncodeFixedPoint(fixedPoint, result);
        if (data.Length == 0) return result.ToArray();
        var ints = new int[3];
        ints[1] = (int)(data[0] * fixedPoint + 0.5);
        for (var i = 0; i < 4; i++) result.Add((byte)((ints[1] >> (i * 8)) & 0xff));
        if (data.Length == 1) return result.ToArray();
        ints[2] = (int)(data[1] * fixedPoint + 0.5);
        for (var i = 0; i < 4; i++) result.Add((byte)((ints[2] >> (i * 8)) & 0xff));
        var halfBytes = new List<byte>();
        for (var i = 2; i < data.Length; i++) {
            ints[0] = ints[1];
            ints[1] = ints[2];
            ints[2] = (int)(data[i] * fixedPoint + 0.5);
            var extrapol = ints[1] + (ints[1] - ints[0]);
            var diff = ints[2] - extrapol;
            EncodeInt(diff, halfBytes);
        }
        PackHalfBytes(halfBytes, result);
        return result.ToArray();
    }

    public static byte[] EncodePic(double[] data) {
        var halfBytes = new List<byte>();
        foreach (var v in data) EncodeInt((int)(v + 0.5), halfBytes);
        var result = new List<byte>();
        PackHalfBytes(halfBytes, result);
        return result.ToArray();
    }

    public static byte[] EncodeSlof(double[] data, double fixedPoint) {
        var result = new List<byte>();
        EncodeFixedPoint(fixedPoint, result);
        foreach (var v in data) {
            var x = (ushort)(Math.Log(v + 1.0) * fixedPoint + 0.5);
            result.Add((byte)(x & 0xff));
            result.Add((byte)(x >> 8));
        }
        return result.ToArray();
    }
}
