using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzGuildAnswerSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildAnswerSend, CzGuildAnswerSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGuildAnswerSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 0);

        var ok = CzGuildAnswerSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(0, packet.Answer);
    }
}
