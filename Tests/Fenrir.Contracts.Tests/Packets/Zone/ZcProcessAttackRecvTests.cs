using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcProcessAttackRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(68, ZcProcessAttackRecv.PayloadSize);
        Assert.Equal(AttackForProtocol.WireSize, ZcProcessAttackRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ProcessAttackRecv, ZcProcessAttackRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesAttackForProtocolWrite()
    {
        var attack = WireTestKit.CreatePopulated<AttackForProtocol>(2);

        Span<byte> buffer = new byte[ZcProcessAttackRecv.PayloadSize];
        var written = attack.Write(buffer);

        Assert.Equal(AttackForProtocol.WireSize, written);

        var packet = new ZcProcessAttackRecv { AttackInfo = attack };
        Span<byte> writeBuffer = new byte[ZcProcessAttackRecv.PayloadSize];
        packet.Write(writeBuffer);

        Assert.True(buffer.SequenceEqual(writeBuffer));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var attack = new AttackForProtocol
        {
            Case = 1,
            ServerIndex1 = 10,
            UniqueNumber1 = 111u,
            ServerIndex2 = 20,
            UniqueNumber2 = 222u,
            SenderLocation = [1f, 2f, 3f],
            AttackActionValue1 = 4,
            AttackActionValue2 = 5,
            AttackActionValue3 = 6,
            AttackActionValue4 = 7,
            AttackResultValue = 1,
            AttackCriticalExist = 0,
            AttackElementDamage = 8,
            AttackViewDamageValue = 100,
            AttackRealDamageValue = 95
        };
        var packet = new ZcProcessAttackRecv { AttackInfo = attack };

        var golden = new byte[ZcProcessAttackRecv.PayloadSize];
        var goldenWritten = WireTestKit.EncodeAttackForProtocol(golden, attack);
        Assert.Equal(AttackForProtocol.WireSize, goldenWritten);

        Span<byte> buffer = new byte[ZcProcessAttackRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
