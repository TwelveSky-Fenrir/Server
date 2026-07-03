using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, GuildInviteAnswerRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildInviteAnswer, GuildInviteAnswerRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[GuildInviteAnswerRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 0);

        var ok = GuildInviteAnswerRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(0, packet.Answer);
    }
}
