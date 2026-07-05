using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, PartyChatResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyChat, PartyChatResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyChatResponse>(1);

        Span<byte> buffer = new byte[PartyChatResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyChatResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal(packet.Content, WireTestKit.ReadFixedString(buffer.Slice(13, 61)));
        Assert.Equal(packet.Link.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(74, 4)));
        Assert.Equal(packet.Link.Activity, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(78, 4)));
        Assert.Equal(packet.Link.Value, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(82, 4)));
        for (var i = 0; i < 3; i++)
            Assert.Equal(packet.Link.Socket[i], BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(86 + i * 4, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyChatResponse>(11);

        var expected = new byte[PartyChatResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyChatResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyChatResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
        WireTestKit.WriteFixedString(destination.Slice(13, 61), value.Content);
        BinaryPrimitives.WriteInt32LittleEndian(destination[74..], value.Link.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[78..], value.Link.Activity);
        BinaryPrimitives.WriteInt32LittleEndian(destination[82..], value.Link.Value);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(86 + i * 4)..], value.Link.Socket[i]);
    }
}
