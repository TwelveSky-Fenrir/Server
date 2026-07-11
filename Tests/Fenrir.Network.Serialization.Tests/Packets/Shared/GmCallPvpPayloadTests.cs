using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

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
