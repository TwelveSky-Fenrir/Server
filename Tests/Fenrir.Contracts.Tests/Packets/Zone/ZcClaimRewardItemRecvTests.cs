using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcClaimRewardItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, ZcClaimRewardItemRecv.PayloadSize);
        Assert.Equal(4 + 24 + 4 + 4 + 4, ZcClaimRewardItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ClaimRewardItemRecv, ZcClaimRewardItemRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        // iSort==99 case: Value[0]=itemID, Value[3]=1, rest 0 (spec note).
        var value = new[] { 5000, 0, 0, 1, 0, 0 };
        var packet = new ZcClaimRewardItemRecv
        {
            Result = 0,
            Value = value,
            InvenPage = 1,
            InvenX = 3,
            InvenY = 5
        };

        var actual = new byte[ZcClaimRewardItemRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcClaimRewardItemRecv.PayloadSize, written);

        var expected = new byte[ZcClaimRewardItemRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        for (var i = 0; i < value.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4 + i * 4), value[i]);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(28), 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(32), 3);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(36), 5);

        Assert.Equal(expected, actual);
    }
}
