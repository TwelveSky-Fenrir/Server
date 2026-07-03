using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcBroadcastWorldInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(3840, ZcBroadcastWorldInfo.PayloadSize);
        Assert.Equal(WorldInfo.WireSize + TribeInfo.WireSize, ZcBroadcastWorldInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.BroadcastWorldInfo, ZcBroadcastWorldInfo.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ThroughNestedWireTypes()
    {
        var worldInfo = WireTestKit.CreatePopulated<WorldInfo>(1);
        var tribeInfo = WireTestKit.CreatePopulated<TribeInfo>(90_000);
        var packet = new ZcBroadcastWorldInfo { WorldInfo = worldInfo, TribeInfo = tribeInfo };

        Span<byte> buffer = new byte[ZcBroadcastWorldInfo.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcBroadcastWorldInfo.PayloadSize, written);

        Assert.True(WorldInfo.TryRead(buffer[..WorldInfo.WireSize], out var worldBack));
        Assert.True(TribeInfo.TryRead(buffer.Slice(WorldInfo.WireSize, TribeInfo.WireSize), out var tribeBack));

        WireTestKit.AssertDeepEqual(worldInfo, worldBack);
        WireTestKit.AssertDeepEqual(tribeInfo, tribeBack);
    }
}
