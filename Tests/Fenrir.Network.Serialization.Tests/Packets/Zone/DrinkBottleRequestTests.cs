using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzBottleStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, DrinkBottleRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DrinkBottle, DrinkBottleRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[DrinkBottleRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 5);

        var ok = DrinkBottleRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(0, packet.Sort);
        Assert.Equal(5, packet.Value);
    }
}
