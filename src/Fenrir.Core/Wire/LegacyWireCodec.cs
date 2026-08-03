using System.Buffers.Binary;
using System.Text;

namespace Fenrir.Core.Wire;

public static class LegacyWireCodec
{
    public static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? source : source[..nul]);
    }

    public static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        var count = Math.Min(value.Length, destination.Length);
        if (count > 0)
            Encoding.Latin1.GetBytes(value.AsSpan(0, count), destination);
    }

    public static string[] ReadFixedStringRows(ReadOnlySpan<byte> source, int rowLength)
    {
        var result = new string[source.Length / rowLength];
        for (var i = 0; i < result.Length; i++)
            result[i] = ReadFixedString(source.Slice(i * rowLength, rowLength));
        return result;
    }

    public static void WriteFixedStringRows(Span<byte> destination, string[] values, int rowLength)
    {
        destination.Clear();
        for (var i = 0; i < values.Length; i++)
            WriteFixedString(destination.Slice(i * rowLength, rowLength), values[i]);
    }

    public static int[] ReadInt32Array(ReadOnlySpan<byte> source)
    {
        var result = new int[source.Length / 4];
        for (var i = 0; i < result.Length; i++)
            result[i] = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(i * 4, 4));
        return result;
    }

    public static void WriteInt32Array(Span<byte> destination, int[] values)
    {
        destination.Clear();
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(i * 4, 4), values[i]);
    }

    public static float[] ReadSingleArray(ReadOnlySpan<byte> source)
    {
        var result = new float[source.Length / 4];
        for (var i = 0; i < result.Length; i++)
            result[i] = BinaryPrimitives.ReadSingleLittleEndian(source.Slice(i * 4, 4));
        return result;
    }

    public static void WriteSingleArray(Span<byte> destination, float[] values)
    {
        destination.Clear();
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(i * 4, 4), values[i]);
    }

    public static byte[] ReadByteArray(ReadOnlySpan<byte> source)
    {
        return source.ToArray();
    }

    public static void WriteByteArray(Span<byte> destination, byte[] values)
    {
        destination.Clear();
        values.AsSpan().CopyTo(destination);
    }
}
