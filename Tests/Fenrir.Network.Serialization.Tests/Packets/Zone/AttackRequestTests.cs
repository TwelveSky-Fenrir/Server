using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzProcessAttackSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(68, AttackRequest.PayloadSize);
        Assert.Equal(AttackForProtocol.WireSize, AttackRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.Attack, AttackRequest.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ThroughAttackForProtocolWrite()
    {
        var attack = WireTestKit.CreatePopulated<AttackForProtocol>(11);

        Span<byte> buffer = new byte[AttackRequest.PayloadSize];
        var written = attack.Write(buffer);

        Assert.Equal(AttackForProtocol.WireSize, written);

        var ok = AttackRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        WireTestKit.AssertDeepEqual(attack, packet.AttackInfo);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var attack = new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = 100,
            UniqueNumber1 = 55_555u,
            ServerIndex2 = 200,
            UniqueNumber2 = 66_666u,
            SenderLocation = [1.5f, 0f, 2.5f],
            AttackActionValue1 = 1,
            AttackActionValue2 = 2,
            AttackActionValue3 = 3,
            AttackActionValue4 = 4,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };

        Span<byte> buffer = new byte[AttackRequest.PayloadSize];
        var written = WireTestKit.EncodeAttackForProtocol(buffer, attack);

        Assert.Equal(AttackForProtocol.WireSize, written);

        var ok = AttackRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        WireTestKit.AssertDeepEqual(attack, packet.AttackInfo);
    }
}
