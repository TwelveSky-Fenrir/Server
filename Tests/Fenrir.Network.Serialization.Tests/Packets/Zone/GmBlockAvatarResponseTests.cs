using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

// Only ever sent on the "gate passed, target not found (or self-targeted)" path -- see the packet's own
// remarks for why a successful block sends the caller nothing at all.
public class GmBlockAvatarResponseTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, GmBlockAvatarResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GmBlockAvatar, GmBlockAvatarResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new GmBlockAvatarResponse { Result = 1 };

        Span<byte> buffer = stackalloc byte[GmBlockAvatarResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GmBlockAvatarResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
