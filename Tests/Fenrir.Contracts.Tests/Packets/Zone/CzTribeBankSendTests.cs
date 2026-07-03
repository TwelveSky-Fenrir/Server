using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTribeBankSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzTribeBankSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeBankSend, CzTribeBankSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzTribeBankSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 17);

        var ok = CzTribeBankSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
        Assert.Equal(17, packet.Value);
    }
}
