using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarRewardGrantSinkTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        return CreateRegistry(ZoneTestKit.EmptyWorldData(), maps);
    }

    private static ZoneRegistry CreateRegistry(WorldDataCache worldData, params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, worldData, []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldDataCache CacheWithBoss561()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [WorldDataTestRows.Monster(RegularWarBossSummonCatalog.BossMonsterId)]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static RegularWarRewardGrant Grant(int characterId, long money = 1000)
    {
        return new RegularWarRewardGrant(characterId, true, money, 0, 0, 0, 0, false);
    }

    [Fact]
    public void OnWarConcluded_EmptyRewards_PostsNothing_DoesNotThrow()
    {
        var registry = CreateRegistry(49);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(49, RegularWarOutcome.AbortedEmptyMap, null, [], false);

        registry[49].Tick(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void OnWarConcluded_UnhostedMap_LogsAndDropsWithoutThrowing()
    {
        var registry = CreateRegistry(49);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(146, RegularWarOutcome.TribeWin, 0,
            [Grant(1)], false);
    }

    [Fact]
    public void OnWarConcluded_HostedMap_PostsOneCommandPerGrant_AppliedNextTick()
    {
        var registry = CreateRegistry(49);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[49].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 49)));
        registry[49].Tick(TimeSpan.FromMilliseconds(50));

        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);
        var rewards = ImmutableArray.Create(Grant(10, 55_000));

        sink.OnWarConcluded(49, RegularWarOutcome.TribeWin, 0, rewards, false);
        registry[49].Tick(TimeSpan.FromMilliseconds(50));

        var grants = registry[49].DrainPendingMoneyGrants();
        Assert.Single(grants);
        Assert.Equal(10, grants[0].CharacterId);
        Assert.Equal(55_000, grants[0].Amount);
    }

    [Fact]
    public void OnWarConcluded_UnknownCharacter_PostsCommandButZoneHandlerNoOps()
    {
        var registry = CreateRegistry(49);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(49, RegularWarOutcome.TribeWin, 0, [Grant(999)], false);
        registry[49].Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(registry[49].DrainPendingMoneyGrants());
    }

    [Fact]
    public void OnWarConcluded_BossMonstersShouldSpawnTrue_PostsSummonCommand_AppliedNextTick()
    {
        var registry = CreateRegistry(CacheWithBoss561(), 295);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(295, RegularWarOutcome.TribeWin, 0, [], true);
        registry[295].Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(RegularWarBossSummonCatalog.SummonCount, registry[295].MonsterCount);
    }

    [Fact]
    public void OnWarConcluded_BossMonstersShouldSpawnFalse_PostsNoSummonCommand_SpawnsNothing()
    {
        var registry = CreateRegistry(CacheWithBoss561(), 295);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(295, RegularWarOutcome.TribeWin, 0, [], false);
        registry[295].Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, registry[295].MonsterCount);
    }

    [Fact]
    public void OnWarConcluded_BossMonstersShouldSpawnTrue_UnhostedMap_LogsAndDropsWithoutThrowing()
    {
        var registry = CreateRegistry(CacheWithBoss561(), 49);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnWarConcluded(295, RegularWarOutcome.TribeWin, 0, [], true);
    }

    [Fact]
    public void EveryOtherLifecycleEvent_IsANoOp_NeverThrows()
    {
        var registry = CreateRegistry(49);
        var sink = new RegularWarRewardGrantSink(registry, NullLogger<RegularWarRewardGrantSink>.Instance);

        sink.OnCountdownAnnounced(49, 5);
        sink.OnSmallestTribeFlagged(49, 2);
        sink.OnActiveWarStarted(49);
        sink.OnMonstersShouldDespawn(49);
        sink.OnAllSessionsShouldDisconnect(49);
    }
}
