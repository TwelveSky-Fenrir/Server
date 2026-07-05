using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFishingStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, FishingLineRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FishingLine, FishingLineRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[FishingLineRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        BinaryPrimitives.WriteSingleLittleEndian(buffer[4..], 123.5f);
        BinaryPrimitives.WriteSingleLittleEndian(buffer[8..], -45.25f);

        var ok = FishingLineRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
        Assert.Equal(123.5f, packet.LocationX);
        Assert.Equal(-45.25f, packet.LocationZ);
    }
}
