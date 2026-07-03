using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcConnectOkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcConnectOkRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ConnectOkRecv, ZcConnectOkRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZcConnectOkRecv { RandomNumber = 0x4E6F7661 };

        Span<byte> buffer = stackalloc byte[ZcConnectOkRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcConnectOkRecv.PayloadSize, written);
        Assert.Equal(packet.RandomNumber, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcConnectOkRecv { RandomNumber = 0x12345678 };

        Span<byte> buffer = stackalloc byte[ZcConnectOkRecv.PayloadSize];
        packet.Write(buffer);

        ReadOnlySpan<byte> golden = [0x78, 0x56, 0x34, 0x12];
        Assert.True(golden.SequenceEqual(buffer));
    }
}
