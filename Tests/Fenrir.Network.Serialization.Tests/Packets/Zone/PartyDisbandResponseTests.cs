using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyBreakRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, PartyDisbandResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyDisband, PartyDisbandResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyDisbandResponse>(1);

        Span<byte> buffer = new byte[PartyDisbandResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyDisbandResponse.PayloadSize, written);

        Assert.Equal(packet.Sort, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyDisbandResponse>(11);

        var expected = new byte[PartyDisbandResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyDisbandResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyDisbandResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Sort);
        WireTestKit.WriteFixedString(destination.Slice(4, 13), value.AvatarName);
    }
}
