using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     <see cref="ZcTribeNotifyRecv.TribeRole" /> actually carries the sender's tribe NUMBER (not a role)
///     per the relay trap documented on the contract; the test only exercises the wire shape.
/// </summary>
public class ZcTribeNotifyRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(78, ZcTribeNotifyRecv.PayloadSize);
        Assert.Equal(4 + 13 + 61, ZcTribeNotifyRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeNotifyRecv, ZcTribeNotifyRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcTribeNotifyRecv { TribeRole = 3, AvatarName = "Odin", Content = "Scroll used!" };

        Span<byte> buffer = new byte[ZcTribeNotifyRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTribeNotifyRecv.PayloadSize, written);

        var golden = new byte[ZcTribeNotifyRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 3);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(17, 61), "Scroll used!");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new ZcTribeNotifyRecv { TribeRole = 4, AvatarName = "Freya", Content = "Tribe number 4" };

        Span<byte> buffer = new byte[ZcTribeNotifyRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal("Tribe number 4", WireTestKit.ReadFixedString(buffer[17..]));
    }
}
