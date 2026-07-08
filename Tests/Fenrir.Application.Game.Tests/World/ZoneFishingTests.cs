using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>Covers <c>Zone.ApplyFishingCommand</c> directly -- the mirror + broadcast half of op 103/104/105.</summary>
public class ZoneFishingTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    [Fact]
    public void Cast_MutatesStateAndBroadcastsToSelfAndNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (caster, casterPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(caster, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20,
            ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(casterPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        var castAt = DateTime.UtcNow;
        zone.PostFishingCommand(new FishingZoneCommand(10, 1, 2, false, true, 92, castAt));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(1, state!.FishingState);
        Assert.Equal(2, state.FishingStep);
        Assert.False(state.CatchingFish);
        Assert.Equal(92, state.ActionSort);
        Assert.Equal(castAt, state.FishingCastAtUtc);

        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(casterPipe).Length);
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Reel_ResetsStateWithNoBroadcast()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (caster, casterPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(caster, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(casterPipe);

        zone.PostFishingCommand(new FishingZoneCommand(10, 1, 2, false, true, 92, DateTime.UtcNow));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(casterPipe);

        zone.PostFishingCommand(new FishingZoneCommand(10, 0, 0, false, false, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(0, state!.FishingState);
        Assert.Equal(0, state.FishingStep);
        Assert.False(state.CatchingFish);
        Assert.Empty(ZoneTestKit.DrainOutbound(casterPipe));
    }

    [Fact]
    public void CatchStep_SetsCatchingFishAndBroadcastsActionSort93()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (caster, casterPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(caster, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(casterPipe);

        zone.PostFishingCommand(new FishingZoneCommand(10, 1, 4, true, true, 93));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(4, state!.FishingStep);
        Assert.True(state.CatchingFish);
        Assert.Equal(93, state.ActionSort);
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(casterPipe).Length);
    }

    [Fact]
    public void UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.PostFishingCommand(new FishingZoneCommand(999, 1, 2, false, true, 92));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
