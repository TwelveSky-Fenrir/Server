namespace Fenrir.Network.Serialization.Wire;

/// <summary>
///     (De)serialization primitives shared by code emitted by Fenrir.Generators.Protocol. Strings use
///     <see cref="System.Text.Encoding.Latin1" /> (wire contract §7.6); scalars are little-endian (via
///     <see cref="System.Buffers.Binary.BinaryPrimitives" />).
/// </summary>
/// <remarks>
///     Hand-written, not generator-emitted: this content used to be regenerated verbatim into every project the
///     Protocol generator was attached to via <c>RuntimeHelpersEmitter</c>/<c>RegisterPostInitializationOutput</c>.
///     A single checked-in copy is simpler to read/diff and is exactly as available to <see cref="MessageReader" />/
///     <see cref="MessageWriter" /> and every generated packet's <c>TryRead</c>/<c>Write</c>.
/// </remarks>
public static class LegacyWireCodec
{
    public static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return System.Text.Encoding.Latin1.GetString(nul < 0 ? source : source[..nul]);
    }

    public static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        var count = Math.Min(value.Length, destination.Length);
        if (count > 0)
            System.Text.Encoding.Latin1.GetBytes(value.AsSpan(0, count), destination);
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
        for (var i = 0; i < values.Length; i++)
            WriteFixedString(destination.Slice(i * rowLength, rowLength), values[i]);
    }

    public static int[] ReadInt32Array(ReadOnlySpan<byte> source)
    {
        var result = new int[source.Length / 4];
        for (var i = 0; i < result.Length; i++)
            result[i] = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.Slice(i * 4, 4));
        return result;
    }

    public static void WriteInt32Array(Span<byte> destination, int[] values)
    {
        for (var i = 0; i < values.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(i * 4, 4), values[i]);
    }

    public static float[] ReadSingleArray(ReadOnlySpan<byte> source)
    {
        var result = new float[source.Length / 4];
        for (var i = 0; i < result.Length; i++)
            result[i] = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(source.Slice(i * 4, 4));
        return result;
    }

    public static void WriteSingleArray(Span<byte> destination, float[] values)
    {
        for (var i = 0; i < values.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(i * 4, 4), values[i]);
    }

    public static byte[] ReadByteArray(ReadOnlySpan<byte> source)
    {
        return source.ToArray();
    }

    public static void WriteByteArray(Span<byte> destination, byte[] values)
    {
        values.AsSpan().CopyTo(destination);
    }
}
