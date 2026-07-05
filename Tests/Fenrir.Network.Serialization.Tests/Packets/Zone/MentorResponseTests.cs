using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTeacherAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, MentorResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Mentor, MentorResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<MentorResponse>(1);

        Span<byte> buffer = new byte[MentorResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(MentorResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<MentorResponse>(11);

        var expected = new byte[MentorResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[MentorResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, MentorResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
