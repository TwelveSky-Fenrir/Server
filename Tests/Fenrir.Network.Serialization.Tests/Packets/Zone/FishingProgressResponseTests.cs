using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFishingResultRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, FishingProgressResponse.PayloadSize);
        Assert.Equal(5 * 4, FishingProgressResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FishingProgress, FishingProgressResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new FishingProgressResponse
        {
            ServerIndex = 7,
            UniqueNumber = 42u,
            Result = 1,
            FishingState = 1,
            FishingStep = 4
        };

        var golden = new byte[FishingProgressResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 7);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 42u);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 4);

        Span<byte> buffer = new byte[FishingProgressResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(FishingProgressResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
