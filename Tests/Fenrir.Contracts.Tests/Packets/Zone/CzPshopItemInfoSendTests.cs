using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPshopItemInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzPshopItemInfoSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PshopItemInfoSend, CzPshopItemInfoSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzPshopItemInfoSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 7);

        var ok = CzPshopItemInfoSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort1);
        Assert.Equal(7, packet.Sort2);
    }
}
