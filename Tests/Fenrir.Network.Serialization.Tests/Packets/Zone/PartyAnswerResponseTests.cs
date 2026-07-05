using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, PartyAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyAnswer, PartyAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyAnswerResponse>(1);

        Span<byte> buffer = new byte[PartyAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyAnswerResponse.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyAnswerResponse>(11);

        var expected = new byte[PartyAnswerResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyAnswerResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyAnswerResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
