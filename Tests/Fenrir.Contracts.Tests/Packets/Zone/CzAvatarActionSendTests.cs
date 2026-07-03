using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzAvatarActionSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, CzAvatarActionSend.PayloadSize);
        Assert.Equal(ActionInfo.WireSize, CzAvatarActionSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AvatarActionSend, CzAvatarActionSend.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ThroughActionInfoWrite()
    {
        var action = WireTestKit.CreatePopulated<ActionInfo>(1);

        Span<byte> buffer = new byte[CzAvatarActionSend.PayloadSize];
        var written = action.Write(buffer);

        Assert.Equal(ActionInfo.WireSize, written);

        var ok = CzAvatarActionSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        WireTestKit.AssertDeepEqual(action, packet.Action);
    }
}
