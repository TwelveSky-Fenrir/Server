using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcBroadcastChugsoungRepairInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, TowerRepairInfoResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TowerRepairInfo, TowerRepairInfoResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TowerRepairInfoResponse>(1);

        Span<byte> buffer = new byte[TowerRepairInfoResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TowerRepairInfoResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TowerRepairInfoResponse>(11);

        var expected = new byte[TowerRepairInfoResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TowerRepairInfoResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TowerRepairInfoResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
