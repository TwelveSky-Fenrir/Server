using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcConnectOkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZoneGreetingResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneGreeting, ZoneGreetingResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZoneGreetingResponse { RandomNumber = 0x4E6F7661 };

        Span<byte> buffer = stackalloc byte[ZoneGreetingResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZoneGreetingResponse.PayloadSize, written);
        Assert.Equal(packet.RandomNumber, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };

        Span<byte> buffer = stackalloc byte[ZoneGreetingResponse.PayloadSize];
        packet.Write(buffer);

        ReadOnlySpan<byte> golden = [0x78, 0x56, 0x34, 0x12];
        Assert.True(golden.SequenceEqual(buffer));
    }
}
