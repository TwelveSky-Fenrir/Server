using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzSetDeputyPshopMoneySendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzSetDeputyPshopMoneySend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.SetDeputyPshopMoneySend, CzSetDeputyPshopMoneySend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzSetDeputyPshopMoneySend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1000);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 5);

        var ok = CzSetDeputyPshopMoneySend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1000, packet.Money);
        Assert.Equal(5, packet.BigMoney);
    }
}
