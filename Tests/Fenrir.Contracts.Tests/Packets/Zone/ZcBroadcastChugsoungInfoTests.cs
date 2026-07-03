using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcBroadcastChugsoungInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(96, ZcBroadcastChugsoungInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.BroadcastChugsoungInfo, ZcBroadcastChugsoungInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[96];
        value.Write(actual);

        var expected = new byte[96];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcBroadcastChugsoungInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZcBroadcastChugsoungInfo
        {
            State1Tower = v.NextIntArray(12),
            State2Tower = v.NextIntArray(12)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZcBroadcastChugsoungInfo value)
    {
        for (var i = 0; i < 12; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.State1Tower[i]);
        for (var i = 0; i < 12; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(48 + i * 4)..], value.State2Tower[i]);
    }
}
