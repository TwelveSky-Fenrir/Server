using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, DuelChallengeResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelChallenge, DuelChallengeResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<DuelChallengeResponse>(1);

        Span<byte> buffer = new byte[DuelChallengeResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(DuelChallengeResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal(packet.Sort, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(13, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<DuelChallengeResponse>(11);

        var expected = new byte[DuelChallengeResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[DuelChallengeResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, DuelChallengeResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
        BinaryPrimitives.WriteInt32LittleEndian(destination[13..], value.Sort);
    }
}
