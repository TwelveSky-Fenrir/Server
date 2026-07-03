using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzClientOkForZoneSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, CzClientOkForZoneSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ClientOkForZoneSend, CzClientOkForZoneSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzClientOkForZoneSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer[..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 111);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 222);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..], 1);

        var ok = CzClientOkForZoneSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Tribe);
        Assert.Equal(111, packet.AutoTime);
        Assert.Equal(222, packet.AutoTime2);
        Assert.Equal(1, packet.AutoState);
    }
}
