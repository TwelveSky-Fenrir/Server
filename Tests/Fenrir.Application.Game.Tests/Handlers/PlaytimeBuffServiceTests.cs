using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class PlaytimeBuffServiceTests
{
    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public void ValidSort_MirrorsStateTimeEffect()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var service = new PlaytimeBuffService();

        service.Apply(zone, 10, 3);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(240, player!.StateTimeEffect);
    }

    [Fact]
    public void OutOfRangeSort_NoMirror()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var service = new PlaytimeBuffService();

        service.Apply(zone, 10, 9);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(0, player!.StateTimeEffect);
    }

    [Fact]
    public void ValidSort_ClobbersPlayTime2ToLegacyConstant()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.PlayTime2 = 42;
        var service = new PlaytimeBuffService();

        service.Apply(zone, 10, 3);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(PlaytimeBuffResolver.PlayTimeClobberValue, player!.PlayTime2);
    }

    [Fact]
    public void OutOfRangeSort_StillClobbersPlayTime2()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.PlayTime2 = 42;
        var service = new PlaytimeBuffService();

        service.Apply(zone, 10, 9);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(PlaytimeBuffResolver.PlayTimeClobberValue, player!.PlayTime2);
    }
}
