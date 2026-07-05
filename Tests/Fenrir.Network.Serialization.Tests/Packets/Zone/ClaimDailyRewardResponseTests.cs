using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcClaimRewardItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, ClaimDailyRewardResponse.PayloadSize);
        Assert.Equal(4 + 24 + 4 + 4 + 4, ClaimDailyRewardResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ClaimDailyReward, ClaimDailyRewardResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        // iSort==99 case: Value[0]=itemID, Value[3]=1, rest 0 (spec note).
        var value = new[] { 5000, 0, 0, 1, 0, 0 };
        var packet = new ClaimDailyRewardResponse
        {
            Result = 0,
            Value = value,
            InvenPage = 1,
            InvenX = 3,
            InvenY = 5
        };

        var actual = new byte[ClaimDailyRewardResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ClaimDailyRewardResponse.PayloadSize, written);

        var expected = new byte[ClaimDailyRewardResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        for (var i = 0; i < value.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4 + i * 4), value[i]);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(28), 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(32), 3);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(36), 5);

        Assert.Equal(expected, actual);
    }
}
