using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_PROCESS_DATA_SEND tSort 701, "GM_CLEAR_INVENTORY" (Server/ts25zone/S04_MyWork04.cpp:2084-2111). Rides
// inside GenericActionRequest's (opcode 19) tData blob -- there is no dedicated legacy wire opcode for this
// command.
public class GmClearInventoryPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, GmClearInventoryPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmClearInventoryPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmClearInventoryPayload.WireSize, written);

        Assert.True(GmClearInventoryPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(2)]
    public void TryRead_DecodesGoldenBytes(int pageSelector)
    {
        var buffer = new byte[GmClearInventoryPayload.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), pageSelector);

        Assert.True(GmClearInventoryPayload.TryRead(buffer, out var payload));

        Assert.Equal(pageSelector, payload.PageSelector);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmClearInventoryPayload.TryRead(new byte[3], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst4BytesOfLargerBuffer()
    {
        // GenericActionHandler reads this out of the first 4 bytes of GenericActionRequest.Data (130 bytes),
        // not a dedicated 4-byte packet.
        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), 1);

        Assert.True(GmClearInventoryPayload.TryRead(data, out var payload));
        Assert.Equal(1, payload.PageSelector);
    }

    private static GmClearInventoryPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmClearInventoryPayload { PageSelector = v.NextInt() };
    }
}
