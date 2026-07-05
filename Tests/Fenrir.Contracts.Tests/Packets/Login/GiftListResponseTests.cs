using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcDemandGiftRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=85 (1-byte outbound header) -> 84-byte payload (int result + int[10][2] flattened).
        Assert.Equal(84, GiftListResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        // V1 shape: [page*2+0]=itemId, [page*2+1]=0 (GIFT_V2 disabled in EU33).
        var giftItem = new int[20];
        for (var page = 0; page < 10; page++)
            giftItem[page * 2] = 1000 + page;

        var packet = new GiftListResponse { Result = 0, GiftItem = giftItem };

        var buffer = new byte[GiftListResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GiftListResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));

        var decoded = new int[20];
        for (var i = 0; i < 20; i++)
            decoded[i] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4 + i * 4, 4));
        Assert.True(packet.GiftItem.SequenceEqual(decoded));
    }
}
