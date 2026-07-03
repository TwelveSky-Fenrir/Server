using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzEndPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzEndPshopSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.EndPshopSend, CzEndPshopSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzEndPshopSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);

        var ok = CzEndPshopSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
    }
}
