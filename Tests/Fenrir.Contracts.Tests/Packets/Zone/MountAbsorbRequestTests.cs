using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzAnimalAbsorbSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, MountAbsorbRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.MountAbsorb, MountAbsorbRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[MountAbsorbRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);

        var ok = MountAbsorbRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
    }
}
