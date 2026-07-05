using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcRegisterAvatarRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(11448, EnterWorldResponse.PayloadSize);
        Assert.Equal(AvatarInfo.WireSize + BuffInfo.WireSize, EnterWorldResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.EnterWorld, EnterWorldResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ThroughNestedWireTypes()
    {
        var avatarInfo = WireTestKit.CreatePopulated<AvatarInfo>(1);
        var buffInfo = WireTestKit.CreatePopulated<BuffInfo>(50_000);
        var packet = new EnterWorldResponse { AvatarInfo = avatarInfo, BuffInfo = buffInfo };

        Span<byte> buffer = new byte[EnterWorldResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(EnterWorldResponse.PayloadSize, written);

        Assert.True(AvatarInfo.TryRead(buffer[..AvatarInfo.WireSize], out var avatarBack));
        Assert.True(BuffInfo.TryRead(buffer.Slice(AvatarInfo.WireSize, BuffInfo.WireSize), out var buffBack));

        WireTestKit.AssertDeepEqual(avatarInfo, avatarBack);
        WireTestKit.AssertDeepEqual(buffInfo, buffBack);
    }
}
