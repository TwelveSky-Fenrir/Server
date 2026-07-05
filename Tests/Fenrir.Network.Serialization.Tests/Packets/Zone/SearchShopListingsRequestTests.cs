using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzPshopItemInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, SearchShopListingsRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.SearchShopListings, SearchShopListingsRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[SearchShopListingsRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 7);

        var ok = SearchShopListingsRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort1);
        Assert.Equal(7, packet.Sort2);
    }
}
