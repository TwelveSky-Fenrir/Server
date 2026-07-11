using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcHighItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, UpgradeItemRankResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UpgradeItemRank, UpgradeItemRankResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[UpgradeItemRankResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static UpgradeItemRankResponse CreatePopulated()
    {
        return new UpgradeItemRankResponse { Result = 11, Cost = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, UpgradeItemRankResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Cost);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
