using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzAnimalAbsorbSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzAnimalAbsorbSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AnimalAbsorbSend, CzAnimalAbsorbSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzAnimalAbsorbSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);

        var ok = CzAnimalAbsorbSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
    }
}
