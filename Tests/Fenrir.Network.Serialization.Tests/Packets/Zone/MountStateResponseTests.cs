using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcAnimalStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, MountStateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MountState, MountStateResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new MountStateResponse { Sort = 3, Value = 0 };

        var golden = new byte[MountStateResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 0);

        Span<byte> buffer = new byte[MountStateResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(MountStateResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new MountStateResponse { Sort = 1, Value = 4 };

        Span<byte> buffer = new byte[MountStateResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));
    }
}
