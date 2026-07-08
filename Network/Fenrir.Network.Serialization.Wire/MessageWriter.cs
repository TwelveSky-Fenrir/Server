using System.Buffers.Binary;

namespace Fenrir.Network.Serialization.Wire;

/// <summary>
///     Zero-allocation, stack-only sequential cursor over an outgoing packet's payload. Every
///     <c>[FenrirPacket]</c>/<c>[FenrirWireType]</c>'s generated <c>Write</c> constructs one of these over its
///     <c>destination</c> span and writes fields in declaration order — mirrors <see cref="MessageReader" />'s
///     own reasoning for why cursor-based emission can never desync from the offsets <c>FieldScanner</c> computes
///     at generator time.
/// </summary>
public ref struct MessageWriter(Span<byte> destination)
{
    private readonly Span<byte> _destination = destination;
    private int _offset;

    /// <summary><c>[Reserved(N)]</c> padding before a field — zero-cleared, matching the legacy wire's own padding convention.</summary>
    public void Skip(int length)
    {
        _destination.Slice(_offset, length).Clear();
        _offset += length;
    }

    /// <summary>
    ///     Escape hatch: reserves <paramref name="length" /> bytes and returns them for the caller to fill directly —
    ///     used for nested <see cref="IFenrirWireType{TSelf}" /> delegation (`nested.Write(span)`) and as the span
    ///     every typed <c>Write*</c> method below writes into and returns, so a field carrying
    ///     <c>[AvatarXorKind]</c>/<c>[ObfuscatedUidField]</c> can XOR the exact bytes it just wrote without
    ///     recomputing an offset.
    /// </summary>
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
