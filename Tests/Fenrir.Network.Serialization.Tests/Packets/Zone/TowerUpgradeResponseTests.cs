using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcChugsoungWarUpRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(24, TowerUpgradeResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TowerUpgrade, TowerUpgradeResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[24];
        value.Write(actual);

        var expected = new byte[24];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static TowerUpgradeResponse CreatePopulated()
    {
        return new TowerUpgradeResponse
        {
            Result = 0,
            Page = [10011, 0],
            Index = [10011, 0],
            Count = 1
        };
    }

    private static void EncodeGolden(Span<byte> destination, TowerUpgradeResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Page[0]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Page[1]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Index[0]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Index[1]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Count);
    }
}
