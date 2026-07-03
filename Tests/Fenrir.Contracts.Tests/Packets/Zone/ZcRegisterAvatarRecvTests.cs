using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcRegisterAvatarRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(11448, ZcRegisterAvatarRecv.PayloadSize);
        Assert.Equal(AvatarInfo.WireSize + BuffInfo.WireSize, ZcRegisterAvatarRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.RegisterAvatarRecv, ZcRegisterAvatarRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ThroughNestedWireTypes()
    {
        var avatarInfo = WireTestKit.CreatePopulated<AvatarInfo>(1);
        var buffInfo = WireTestKit.CreatePopulated<BuffInfo>(50_000);
        var packet = new ZcRegisterAvatarRecv { AvatarInfo = avatarInfo, BuffInfo = buffInfo };

        Span<byte> buffer = new byte[ZcRegisterAvatarRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcRegisterAvatarRecv.PayloadSize, written);

        Assert.True(AvatarInfo.TryRead(buffer[..AvatarInfo.WireSize], out var avatarBack));
        Assert.True(BuffInfo.TryRead(buffer.Slice(AvatarInfo.WireSize, BuffInfo.WireSize), out var buffBack));

        WireTestKit.AssertDeepEqual(avatarInfo, avatarBack);
        WireTestKit.AssertDeepEqual(buffInfo, buffBack);
    }
}
