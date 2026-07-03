using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     CZ_EXCHANGE_ITEM_SEND (CLIENT.h:232-242, 20-byte payload): Sort/Page1/Index1/Value1/Value2. No
///     <c>tTribe</c> field (USE_EXCHANGE_ITEM_V2 not compiled in EU33) — locking the 20-byte payload.
/// </summary>
public class CzExchangeItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, CzExchangeItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ExchangeItemSend, CzExchangeItemSend.Opcode);
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

        Assert.True(CzExchangeItemSend.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Sort);
        Assert.Equal(22, decoded.Page1);
        Assert.Equal(33, decoded.Index1);
        Assert.Equal(44, decoded.Value1);
        Assert.Equal(55, decoded.Value2);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzExchangeItemSend.TryRead(new byte[19], out _));
    }
}
