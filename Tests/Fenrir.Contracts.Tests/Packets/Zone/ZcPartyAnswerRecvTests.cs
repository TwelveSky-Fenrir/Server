using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcPartyAnswerRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyAnswerRecv, ZcPartyAnswerRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyAnswerRecv>(1);

        Span<byte> buffer = new byte[ZcPartyAnswerRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcPartyAnswerRecv.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyAnswerRecv>(11);

        var expected = new byte[ZcPartyAnswerRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcPartyAnswerRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcPartyAnswerRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
