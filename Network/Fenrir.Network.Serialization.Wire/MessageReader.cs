using System.Buffers.Binary;

namespace Fenrir.Network.Serialization.Wire;

public ref struct MessageReader(ReadOnlySpan<byte> source)
{
    private readonly ReadOnlySpan<byte> _source = source;
    private int _offset;

        public void Skip(int length)
    {
        _offset += length;
    }

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
