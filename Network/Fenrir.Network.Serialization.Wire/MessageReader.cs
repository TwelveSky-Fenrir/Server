using System.Buffers.Binary;

namespace Fenrir.Network.Serialization.Wire;

/// <summary>
///     Zero-allocation, stack-only sequential cursor over an incoming packet's payload. Every
///     <c>[FenrirPacket]</c>/<c>[FenrirWireType]</c>'s generated <c>TryRead</c> constructs one of these over its
///     <c>source</c> span and reads fields in declaration order — the cursor's own running position IS the field
///     offset, so there is exactly one place that computes it (here), never duplicated against the offsets
///     <c>FieldScanner</c> computes at generator time for <c>PayloadSize</c>/<c>ExpectedSize</c> bookkeeping.
/// </summary>
public ref struct MessageReader(ReadOnlySpan<byte> source)
{
    private readonly ReadOnlySpan<byte> _source = source;
    private int _offset;

    /// <summary><c>[Reserved(N)]</c> padding before a field — the bytes are never meaningful, only skipped.</summary>
    public void Skip(int length)
    {
        _offset += length;
    }

    /// <summary>Escape hatch for nested <see cref="IFenrirWireType{TSelf}" /> fields (`TryRead` needs a span, not a cursor).</summary>
    public ReadOnlySpan<byte> ReadSlice(int length)
    {
        var slice = _source.Slice(_offset, length);
        _offset += length;
        return slice;
    }

    public byte ReadByte()
    {
        return _source[_offset++];
    }

    public int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_source.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_source.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public long ReadInt64()
    {
        var value = BinaryPrimitives.ReadInt64LittleEndian(_source.Slice(_offset, 8));
        _offset += 8;
        return value;
    }

    public float ReadSingle()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(_source.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public string ReadFixedString(int length)
    {
        return LegacyWireCodec.ReadFixedString(ReadSlice(length));
    }

    public int[] ReadInt32Array(int byteLength)
    {
        return LegacyWireCodec.ReadInt32Array(ReadSlice(byteLength));
    }

    public float[] ReadSingleArray(int byteLength)
    {
        return LegacyWireCodec.ReadSingleArray(ReadSlice(byteLength));
    }

    public byte[] ReadByteArray(int byteLength)
    {
        return LegacyWireCodec.ReadByteArray(ReadSlice(byteLength));
    }

    public string[] ReadFixedStringRows(int byteLength, int rowLength)
    {
        return LegacyWireCodec.ReadFixedStringRows(ReadSlice(byteLength), rowLength);
    }
}
