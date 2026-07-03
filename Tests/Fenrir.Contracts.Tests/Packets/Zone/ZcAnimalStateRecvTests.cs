using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcAnimalStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, ZcAnimalStateRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AnimalStateRecv, ZcAnimalStateRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcAnimalStateRecv { Sort = 3, Value = 0 };

        var golden = new byte[ZcAnimalStateRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 0);

        Span<byte> buffer = new byte[ZcAnimalStateRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcAnimalStateRecv.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZcAnimalStateRecv { Sort = 1, Value = 4 };

        Span<byte> buffer = new byte[ZcAnimalStateRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));
    }
}
