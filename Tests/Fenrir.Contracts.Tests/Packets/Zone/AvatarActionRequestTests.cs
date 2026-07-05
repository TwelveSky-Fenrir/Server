using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzAvatarActionSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, AvatarActionRequest.PayloadSize);
        Assert.Equal(ActionInfo.WireSize, AvatarActionRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AvatarAction, AvatarActionRequest.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ThroughActionInfoWrite()
    {
        var action = WireTestKit.CreatePopulated<ActionInfo>(1);

        Span<byte> buffer = new byte[AvatarActionRequest.PayloadSize];
        var written = action.Write(buffer);

        Assert.Equal(ActionInfo.WireSize, written);

        var ok = AvatarActionRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        WireTestKit.AssertDeepEqual(action, packet.Action);
    }
}
