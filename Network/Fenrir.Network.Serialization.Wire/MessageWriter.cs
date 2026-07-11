using System.Buffers.Binary;

namespace Fenrir.Network.Serialization.Wire;

public ref struct MessageWriter(Span<byte> destination)
{
    private readonly Span<byte> _destination = destination;
    private int _offset;

    public void Skip(int length)
    {
        _destination.Slice(_offset, length).Clear();
        _offset += length;
    }

    public Span<byte> Reserve(int length)
    {
        var slice = _destination.Slice(_offset, length);
        _offset += length;
        return slice;
    }

    public Span<byte> WriteByte(byte value)
    {
        var slice = Reserve(1);
        slice[0] = value;
        return slice;
    }

    public Span<byte> WriteInt32(int value)
    {
        var slice = Reserve(4);
        BinaryPrimitives.WriteInt32LittleEndian(slice, value);
        return slice;
    }

    public Span<byte> WriteUInt32(uint value)
    {
        var slice = Reserve(4);
        BinaryPrimitives.WriteUInt32LittleEndian(slice, value);
        return slice;
    }

    public Span<byte> WriteInt64(long value)
    {
        var slice = Reserve(8);
        BinaryPrimitives.WriteInt64LittleEndian(slice, value);
        return slice;
    }

    public Span<byte> WriteSingle(float value)
    {
        var slice = Reserve(4);
        BinaryPrimitives.WriteSingleLittleEndian(slice, value);
        return slice;
    }

    public Span<byte> WriteFixedString(string value, int length)
    {
        var slice = Reserve(length);
        LegacyWireCodec.WriteFixedString(slice, value);
        return slice;
    }

    public Span<byte> WriteInt32Array(int[] values, int byteLength)
    {
        var slice = Reserve(byteLength);
        LegacyWireCodec.WriteInt32Array(slice, values);
        return slice;
    }

    public Span<byte> WriteSingleArray(float[] values, int byteLength)
    {
        var slice = Reserve(byteLength);
        LegacyWireCodec.WriteSingleArray(slice, values);
        return slice;
    }

    public Span<byte> WriteByteArray(byte[] values, int byteLength)
    {
        var slice = Reserve(byteLength);
        LegacyWireCodec.WriteByteArray(slice, values);
        return slice;
    }

    public Span<byte> WriteFixedStringRows(string[] values, int byteLength, int rowLength)
    {
        var slice = Reserve(byteLength);
        LegacyWireCodec.WriteFixedStringRows(slice, values, rowLength);
        return slice;
    }
}
