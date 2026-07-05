using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFishingResultSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, FishingProgressRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FishingProgress, FishingProgressRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[FishingProgressRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 4);

        var ok = FishingProgressRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort);
        Assert.Equal(4, packet.FishingStep);
    }
}
