using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_PROCESS_DATA_SEND tSort 598, "GM-SETPVPPOINT" (Server/ts25zone/S04_MyWork04.cpp:1755-1769). Rides inside
// GenericActionRequest's (opcode 19) tData blob -- there is no dedicated legacy wire opcode for this command.
public class GmSetPvpPointPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(8, GmSetPvpPointPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmSetPvpPointPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmSetPvpPointPayload.WireSize, written);

        Assert.True(GmSetPvpPointPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[GmSetPvpPointPayload.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 777);

        Assert.True(GmSetPvpPointPayload.TryRead(buffer, out var payload));

        Assert.Equal(2, payload.DuelSlot);
        Assert.Equal(777, payload.PointValue);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmSetPvpPointPayload.TryRead(new byte[7], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst8BytesOfLargerBuffer()
    {
        // GenericActionHandler reads this out of the first 8 bytes of GenericActionRequest.Data (130 bytes),
        // not a dedicated 8-byte packet.
        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), 42);

        Assert.True(GmSetPvpPointPayload.TryRead(data, out var payload));
        Assert.Equal(1, payload.DuelSlot);
        Assert.Equal(42, payload.PointValue);
    }

    private static GmSetPvpPointPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmSetPvpPointPayload { DuelSlot = v.NextInt(), PointValue = v.NextInt() };
    }
}
