using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc049TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, ZoneWar049StatusResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneWar049Status, ZoneWar049StatusResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[20];
        value.Write(actual);

        var expected = new byte[20];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZoneWar049StatusResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZoneWar049StatusResponse
        {
            TribeUserNum = v.NextIntArray(4),
            RemainTime = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZoneWar049StatusResponse value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.TribeUserNum[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.RemainTime);
    }
}
