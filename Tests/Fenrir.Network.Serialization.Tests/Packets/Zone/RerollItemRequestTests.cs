using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

// No tTribe field: USE_EXCHANGE_ITEM_V2 wasn't compiled in EU33, so payload stays 20 bytes.
public class CzExchangeItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, RerollItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.RerollItem, RerollItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 55);

        Assert.True(RerollItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Sort);
        Assert.Equal(22, decoded.Page1);
        Assert.Equal(33, decoded.Index1);
        Assert.Equal(44, decoded.Value1);
        Assert.Equal(55, decoded.Value2);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(RerollItemRequest.TryRead(new byte[19], out _));
    }
}
