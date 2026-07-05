using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcBroadcastWorldInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(3840, WorldSnapshotResponse.PayloadSize);
        Assert.Equal(WorldInfo.WireSize + TribeInfo.WireSize, WorldSnapshotResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.WorldSnapshot, WorldSnapshotResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ThroughNestedWireTypes()
    {
        var worldInfo = WireTestKit.CreatePopulated<WorldInfo>(1);
        var tribeInfo = WireTestKit.CreatePopulated<TribeInfo>(90_000);
        var packet = new WorldSnapshotResponse { WorldInfo = worldInfo, TribeInfo = tribeInfo };

        Span<byte> buffer = new byte[WorldSnapshotResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(WorldSnapshotResponse.PayloadSize, written);

        Assert.True(WorldInfo.TryRead(buffer[..WorldInfo.WireSize], out var worldBack));
        Assert.True(TribeInfo.TryRead(buffer.Slice(WorldInfo.WireSize, TribeInfo.WireSize), out var tribeBack));

        WireTestKit.AssertDeepEqual(worldInfo, worldBack);
        WireTestKit.AssertDeepEqual(tribeInfo, tribeBack);
    }
}
