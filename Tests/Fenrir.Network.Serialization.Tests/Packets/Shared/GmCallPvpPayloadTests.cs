using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_PROCESS_DATA_SEND tSort 599, "GM-CALLPVP" (Server/ts25zone/S04_MyWork04.cpp:1770-1823). Rides inside
// GenericActionRequest's (opcode 19) tData blob -- there is no dedicated legacy wire opcode for this command.
public class GmCallPvpPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(17, GmCallPvpPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmCallPvpPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmCallPvpPayload.WireSize, written);

        Assert.True(GmCallPvpPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[GmCallPvpPayload.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1);
        Encoding.Latin1.GetBytes("Wanderer", buffer.AsSpan(4, 13));

        Assert.True(GmCallPvpPayload.TryRead(buffer, out var payload));

        Assert.Equal(1, payload.DuelSlot);
        Assert.Equal("Wanderer", payload.TargetName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmCallPvpPayload.TryRead(new byte[16], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst17BytesOfLargerBuffer()
    {
        // GenericActionHandler reads this out of the first 17 bytes of GenericActionRequest.Data (130 bytes),
        // not a dedicated 17-byte packet.
        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), 2);
        Encoding.Latin1.GetBytes("Target", data.AsSpan(4, 13));

        Assert.True(GmCallPvpPayload.TryRead(data, out var payload));
        Assert.Equal(2, payload.DuelSlot);
        Assert.Equal("Target", payload.TargetName);
    }

    private static GmCallPvpPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmCallPvpPayload { DuelSlot = v.NextInt(), TargetName = v.NextString(13) };
    }
}
