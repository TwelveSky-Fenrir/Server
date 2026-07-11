using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the C16 wiring gap this workstream closes: <c>MonsterSpawnScheduler.ProcessDeath</c> forwarding to
///     <see cref="PopupEventRewardSystem.NotifyMonsterKill" /> with legacy's own drop-eligibility flag
///     (<see cref="Fenrir.Application.Game.Domain.World.Loot.MonsterDropRoller.IsEligible" />, reused rather than
///     re-derived) -- previously never called from anywhere, leaving the popup system's Monster/PvE counter
///     permanently frozen. <c>PopupEventRewardSystemTests</c> already covers
///     <see cref="PopupEventRewardSystem" />'s own counting/threshold/reset logic exhaustively via direct
///     <c>NotifyMonsterKill</c> calls; this suite instead proves the real kill pipeline actually reaches it, and
///     that the dropEligible flag threaded through is the SAME one the loot pipeline itself uses -- a
///     MartialItemLevel &gt;= 1 monster (a boss/martial-type exemption) never advances the popup counter, an
///     ordinary one does.
/// </summary>
public class MonsterSpawnSchedulerPopupEventWiringTests
{
    /// <summary>145 -- one of <c>PopupEventZoneCatalog.MonsterMaps</c>.</summary>
    private const short MonsterPopupMap = 145;

    private static WorldDataCache CacheWithOneRegion(short martialItemLevel = 0)
    {
        var monster = WorldDataTestRows.Monster(500) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 0,
            MartialItemLevel = martialItemLevel,
            // Instant respawn: the very next Tick call's own respawn scan (10s cadence) re-pops this same slot,
            // letting one test re-kill it 400 times without waiting out a real cooldown.
            SummonTime1 = 0,
            SummonTime2 = 0
        };
        var region = WorldDataTestRows.SpawnRegion(1, MonsterPopupMap, 500) with
        {
            Number = 1, LocationX = 100, LocationY = 0, LocationZ = 100, Radius = 5
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, PlayerRuntimeState Killer) SetUp(WorldDataCache cache)
    {
        var flags = new PopupEventState();
        flags.SetEnabled(PopupEventType.MonsterPve, true);
        var popupSystem = new PopupEventRewardSystem(flags);
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(MonsterPopupMap, simulationSystems: [scheduler, popupSystem],
            worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        // Level 1, matching the monster's ItemLevel 1 -- comfortably inside MonsterDropRoller.IsEligible's own
        // "killer at most 9 levels above the monster's ItemLevel" window.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, MonsterPopupMap, "Killer", level: 1)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        Assert.True(zone.TryGetPlayer(10, out var killer));
        return (zone, killer!);
    }

    /// <summary>
    ///     Kills monster server-index 1 and, within the SAME <see cref="Zone.Tick" /> call, respawns it: the
    ///     10-second span covers both the 0s <c>SummonTime1/2</c> cooldown and the 20-legacy-tick (10s) respawn
    ///     scan cadence, so <see cref="MonsterSpawnScheduler.Simulate" /> drains the death (forwarding the C16
    ///     popup notify) and re-pops the same slot in one call.
    /// </summary>
    private static void KillAndRespawn(Zone zone)
    {
        zone.TryDamageMonster(1, 10_000, 10, out var died, out _);
        Assert.True(died);
        zone.Tick(TimeSpan.FromSeconds(10));
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void EligibleKills_AdvanceThePopupCounter_AndFireTheRewardAtFourHundred()
    {
        // MartialItemLevel 0 -> MonsterDropRoller.IsEligible(monster, killerLevel: 1) == true.
        var (zone, killer) = SetUp(CacheWithOneRegion());

        for (var i = 0; i < 399; i++)
            KillAndRespawn(zone);
        Assert.Equal(0, killer.WarPoint); // 399 < 400 -- not yet

        KillAndRespawn(zone); // 400th kill crosses the Monster/PvE threshold
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void DropIneligibleKills_NeverAdvanceThePopupCounter()
    {
        // MartialItemLevel >= 1 makes MonsterDropRoller.IsEligible unconditionally false (the boss/martial-item
        // exemption), regardless of the killer/monster level gap -- the SAME flag NotifyPopupEventMonsterKill
        // is given, so the popup counter must never advance for any of these 400 kills.
        var (zone, killer) = SetUp(CacheWithOneRegion(martialItemLevel: 1));

        for (var i = 0; i < 400; i++)
            KillAndRespawn(zone);

        Assert.Equal(0, killer.WarPoint);
    }
}
