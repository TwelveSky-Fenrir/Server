using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzProcessQuestSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, CzProcessQuestSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ProcessQuestSend, CzProcessQuestSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzProcessQuestSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..], 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], 5);

        var ok = CzProcessQuestSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
        Assert.Equal(2, packet.Page1);
        Assert.Equal(3, packet.Index1);
        Assert.Equal(4, packet.XPost);
        Assert.Equal(5, packet.YPost);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzProcessQuestSend.TryRead(new byte[19], out _));
    }
}
