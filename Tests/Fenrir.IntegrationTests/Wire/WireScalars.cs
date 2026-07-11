using System.Buffers.Binary;
using System.Text;

namespace Fenrir.IntegrationTests.Wire;

internal static class WireScalars
{
    public static void WriteInt32(Span<byte> destination, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value);
    }

    public static void WriteUInt32(Span<byte> destination, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    }

    public static int ReadInt32(ReadOnlySpan<byte> source)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(source);
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> source)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(source);
    }

    public static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        var count = Math.Min(value.Length, destination.Length);
        if (count > 0)
            Encoding.Latin1.GetBytes(value.AsSpan(0, count), destination);
    }

    public static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? source : source[..nul]);
    }
}
