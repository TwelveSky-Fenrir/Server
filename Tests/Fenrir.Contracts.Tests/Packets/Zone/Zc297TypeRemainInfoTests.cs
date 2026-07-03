using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc297TypeRemainInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, Zc297TypeRemainInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone297TypeRemainInfo, Zc297TypeRemainInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new Zc297TypeRemainInfo { Value00 = 1, Value01 = 2, Value02 = 3 };

        var actual = new byte[12];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 3);

        Assert.Equal(expected, actual);
    }
}
