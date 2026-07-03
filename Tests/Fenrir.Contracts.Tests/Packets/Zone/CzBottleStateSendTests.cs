using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzBottleStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzBottleStateSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.BottleStateSend, CzBottleStateSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzBottleStateSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 5);

        var ok = CzBottleStateSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(0, packet.Sort);
        Assert.Equal(5, packet.Value);
    }
}
