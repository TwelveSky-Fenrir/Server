using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDemandZoneServerInfo2ResultTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(24, ZoneMoveResponse.PayloadSize);
        Assert.Equal(4 + 16 + 4, ZoneMoveResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneMove, ZoneMoveResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZoneMoveResponse { Result = 0, Ip = "192.168.0.10", Port = 9010 };

        Span<byte> buffer = new byte[ZoneMoveResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZoneMoveResponse.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("192.168.0.10", WireTestKit.ReadFixedString(buffer.Slice(4, 16)));
        Assert.Equal(9010, BinaryPrimitives.ReadInt32LittleEndian(buffer[20..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZoneMoveResponse { Result = 1, Ip = "10.0.0.1", Port = 7777 };

        var golden = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 16), "10.0.0.1");
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(20), 7777);

        Span<byte> buffer = new byte[ZoneMoveResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
