using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzCostumeState2SendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CostumeVisibilityRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.CostumeVisibility, CostumeVisibilityRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CostumeVisibilityRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);

        var ok = CostumeVisibilityRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
    }
}
