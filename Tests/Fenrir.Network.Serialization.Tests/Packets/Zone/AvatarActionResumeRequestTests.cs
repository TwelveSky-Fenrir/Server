using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzUpdateAvatarActionTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, AvatarActionResumeRequest.PayloadSize);
        Assert.Equal(ActionInfo.WireSize, AvatarActionResumeRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AvatarActionResume, AvatarActionResumeRequest.Opcode);
    }

    [Fact]
    public void Opcode_DiffersFromAvatarActionSend()
    {
        Assert.NotEqual(AvatarActionRequest.Opcode, AvatarActionResumeRequest.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ThroughActionInfoWrite()
    {
        var action = WireTestKit.CreatePopulated<ActionInfo>(42);

        Span<byte> buffer = new byte[AvatarActionResumeRequest.PayloadSize];
        var written = action.Write(buffer);

        Assert.Equal(ActionInfo.WireSize, written);

        var ok = AvatarActionResumeRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        WireTestKit.AssertDeepEqual(action, packet.Action);
    }
}
