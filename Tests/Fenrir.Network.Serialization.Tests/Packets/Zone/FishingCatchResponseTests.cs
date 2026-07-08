using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFishingRewardRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, FishingCatchResponse.PayloadSize);
        Assert.Equal(5 * 4, FishingCatchResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FishingCatch, FishingCatchResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new FishingCatchResponse
        {
            Result = 1,
            ItemIndex = 9001,
            Page = 0,
            Index = 5,
            XY = 300 // legacy quirk: carries tPosY, not a packed X/Y pair.
        };

        var golden = new byte[FishingCatchResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 9001);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 5);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 300);

        Span<byte> buffer = new byte[FishingCatchResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(FishingCatchResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
