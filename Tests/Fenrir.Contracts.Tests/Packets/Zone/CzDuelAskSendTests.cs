using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDuelAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, CzDuelAskSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DuelAskSend, CzDuelAskSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzDuelAskSend.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(13), 1021);

        var ok = CzDuelAskSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
        Assert.Equal(1021, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzDuelAskSend.TryRead(new byte[16], out _));
    }
}
