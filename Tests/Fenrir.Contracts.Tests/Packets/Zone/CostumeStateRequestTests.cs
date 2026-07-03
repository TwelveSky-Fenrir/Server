using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzCostumeStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CostumeStateRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.CostumeState, CostumeStateRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CostumeStateRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 7);

        var ok = CostumeStateRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort);
        Assert.Equal(7, packet.Value);
    }
}
