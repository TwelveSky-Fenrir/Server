using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFishingStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, FishingLineResponse.PayloadSize);
        Assert.Equal(5 * 4, FishingLineResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FishingLine, FishingLineResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new FishingLineResponse
        {
            ServerIndex = 12,
            UniqueNumber = 0xC0FFEEu,
            Result = 1,
            FishingState = 1,
            FishingStep = 2
        };

        var golden = new byte[FishingLineResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 12);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 0xC0FFEEu);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 2);

        Span<byte> buffer = new byte[FishingLineResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(FishingLineResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
