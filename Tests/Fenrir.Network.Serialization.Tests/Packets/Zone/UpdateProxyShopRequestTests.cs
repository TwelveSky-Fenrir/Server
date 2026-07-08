using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzSetDeputyPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(73, UpdateProxyShopRequest.PayloadSize);
        Assert.Equal(13 + 12 * 4 + 12, UpdateProxyShopRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.UpdateProxyShop, UpdateProxyShopRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[UpdateProxyShopRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer.Slice(0, 13), "Sif");
        BinaryPrimitives.WriteInt32LittleEndian(buffer[13..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[17..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[21..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[25..], 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[29..], 5);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[33..], 6);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[37..], 7);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[41..], 8);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[45..], 9);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[49..], 10);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[53..], 11);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[57..], 12);
        var socket = new[] { 100, 200, 300 };
        for (var i = 0; i < socket.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(61 + i * 4)..], socket[i]);

        var ok = UpdateProxyShopRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Sif", packet.AvatarName);
        Assert.Equal(1, packet.SellPage);
        Assert.Equal(2, packet.SellIndex);
        Assert.Equal(3, packet.SellItemIndex);
        Assert.Equal(4, packet.SelfPage);
        Assert.Equal(5, packet.SelfIndex);
        Assert.Equal(6, packet.SelfX);
        Assert.Equal(7, packet.SelfY);
        Assert.Equal(8, packet.BuySort);
        Assert.Equal(9, packet.Quantity);
        Assert.Equal(10, packet.Value);
        Assert.Equal(11, packet.Serial);
        Assert.Equal(12, packet.Price);
        Assert.True(socket.AsSpan().SequenceEqual(packet.Socket));
    }
}
