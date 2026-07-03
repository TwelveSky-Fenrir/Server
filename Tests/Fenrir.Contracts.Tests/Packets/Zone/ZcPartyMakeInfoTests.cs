using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyMakeInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(69, ZcPartyMakeInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyMakeInfo, ZcPartyMakeInfo.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyMakeInfo>(1);

        Span<byte> buffer = new byte[ZcPartyMakeInfo.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcPartyMakeInfo.PayloadSize, written);

        Assert.Equal(packet.Sort, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.AvatarName01, WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal(packet.AvatarName02, WireTestKit.ReadFixedString(buffer.Slice(17, 13)));
        Assert.Equal(packet.AvatarName03, WireTestKit.ReadFixedString(buffer.Slice(30, 13)));
        Assert.Equal(packet.AvatarName04, WireTestKit.ReadFixedString(buffer.Slice(43, 13)));
        Assert.Equal(packet.AvatarName05, WireTestKit.ReadFixedString(buffer.Slice(56, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyMakeInfo>(11);

        var expected = new byte[ZcPartyMakeInfo.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcPartyMakeInfo.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcPartyMakeInfo value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Sort);
        WireTestKit.WriteFixedString(destination.Slice(4, 13), value.AvatarName01);
        WireTestKit.WriteFixedString(destination.Slice(17, 13), value.AvatarName02);
        WireTestKit.WriteFixedString(destination.Slice(30, 13), value.AvatarName03);
        WireTestKit.WriteFixedString(destination.Slice(43, 13), value.AvatarName04);
        WireTestKit.WriteFixedString(destination.Slice(56, 13), value.AvatarName05);
    }
}
