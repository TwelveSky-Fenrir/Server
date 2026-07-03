using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcStartPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(1236, ZcStartPshopRecv.PayloadSize);
        Assert.Equal(4 + PshopInfo.WireSize, ZcStartPshopRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.StartPshopRecv, ZcStartPshopRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(2);
        var packet = new ZcStartPshopRecv { Result = 0, PshopInfo = shop };

        Span<byte> buffer = new byte[ZcStartPshopRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcStartPshopRecv.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));

        var ok = PshopInfo.TryRead(buffer.Slice(4, PshopInfo.WireSize), out var shopBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(shop, shopBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(11);
        var packet = new ZcStartPshopRecv { Result = 103, PshopInfo = shop };

        var golden = new byte[ZcStartPshopRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 103);
        var shopWritten = WireTestKit.EncodePshopInfo(golden.AsSpan(4), shop);
        Assert.Equal(PshopInfo.WireSize, shopWritten);

        Span<byte> buffer = new byte[ZcStartPshopRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
