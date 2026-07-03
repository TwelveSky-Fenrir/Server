using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFishingRewardRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, ZcFishingRewardRecv.PayloadSize);
        Assert.Equal(5 * 4, ZcFishingRewardRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FishingRewardRecv, ZcFishingRewardRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcFishingRewardRecv
        {
            Result = 1,
            ItemIndex = 9001,
            Page = 0,
            Index = 5,
            XY = 300 // legacy quirk: carries tPosY, not a packed X/Y pair.
        };

        var golden = new byte[ZcFishingRewardRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 9001);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 5);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 300);

        Span<byte> buffer = new byte[ZcFishingRewardRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcFishingRewardRecv.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
