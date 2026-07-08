using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTradeMenuRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, TradeLockResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeLock, TradeLockResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TradeLockResponse>(1);

        Span<byte> buffer = new byte[TradeLockResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TradeLockResponse.PayloadSize, written);

        Assert.Equal(packet.CheckMe, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TradeLockResponse>(11);

        var expected = new byte[TradeLockResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TradeLockResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TradeLockResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.CheckMe);
    }
}
