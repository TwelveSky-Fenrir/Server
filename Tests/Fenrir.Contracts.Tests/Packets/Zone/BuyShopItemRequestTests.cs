using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzBuyPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(45, BuyShopItemRequest.PayloadSize);
        Assert.Equal(4 + 13 + 7 * 4, BuyShopItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.BuyShopItem, BuyShopItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[BuyShopItemRequest.PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, 0xABCDEF01u);
        WireTestKit.WriteFixedString(buffer.Slice(4, 13), "Freyja");
        BinaryPrimitives.WriteInt32LittleEndian(buffer[17..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[21..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[25..], 5);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[29..], 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[33..], 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[37..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[41..], 6);

        var ok = BuyShopItemRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(0xABCDEF01u, packet.UniqueNumber);
        Assert.Equal("Freyja", packet.AvatarName);
        Assert.Equal(1, packet.Page1);
        Assert.Equal(2, packet.Index1);
        Assert.Equal(5, packet.Quantity1);
        Assert.Equal(0, packet.Page2);
        Assert.Equal(4, packet.Index2);
        Assert.Equal(3, packet.XPost2);
        Assert.Equal(6, packet.YPost2);
    }
}
