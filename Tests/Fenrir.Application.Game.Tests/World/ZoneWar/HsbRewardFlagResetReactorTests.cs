using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class HsbRewardFlagResetReactorTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void Apply_ResetsTheFlag_ForEveryReadyPlayer_AcrossEveryZoneThisShardHosts()
    {
        var registry = CreateRegistry(1, 2);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));

        registry[1].TryGetPlayer(10, out var playerA);
        registry[2].TryGetPlayer(20, out var playerB);
        playerA!.HsbStoneRewardClaimed = true;
        playerB!.HsbStoneRewardClaimed = true;

        HsbRewardFlagResetReactor.Apply(registry);

        Assert.False(playerA.HsbStoneRewardClaimed);
        Assert.False(playerB.HsbStoneRewardClaimed);
    }

    [Fact]
    public void Apply_OnAZoneWithNoPlayers_IsANoOp()
    {
        var registry = CreateRegistry(1);

        var exception = Record.Exception(() => HsbRewardFlagResetReactor.Apply(registry));

        Assert.Null(exception);
    }

    [Fact]
    public void NewPlayerRuntimeState_DefaultsToEligible()
    {
        var registry = CreateRegistry(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));

        registry[1].TryGetPlayer(10, out var player);

        Assert.False(player!.HsbStoneRewardClaimed);
    }
}
