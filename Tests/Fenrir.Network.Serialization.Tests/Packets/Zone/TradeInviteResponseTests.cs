using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTradeAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, TradeInviteResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeInvite, TradeInviteResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TradeInviteResponse>(1);

        Span<byte> buffer = new byte[TradeInviteResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TradeInviteResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal(packet.Level, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(13, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TradeInviteResponse>(11);

        var expected = new byte[TradeInviteResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TradeInviteResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TradeInviteResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
        BinaryPrimitives.WriteInt32LittleEndian(destination[13..], value.Level);
    }
}
