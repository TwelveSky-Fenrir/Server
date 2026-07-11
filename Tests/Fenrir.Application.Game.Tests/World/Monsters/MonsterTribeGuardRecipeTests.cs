using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the Tribe Guard direct-melee acquisition recipe (behavior contract
///     <c>monster-ai-recipe-bodies</c>, recovered 2026-07-11): <see cref="MonsterSpecialSort.Derive" />'s new
///     <c>Type</c> 6/7/8/9 -&gt; <see cref="MonsterSpecialSort.TribeGuard" /> mapping exercised end-to-end with
///     <see cref="MonsterAiSystem" />'s own <c>RunGuardDecision</c> recipe body: melee-range-only direct
///     acquisition, no chase/pathing step, and no anti-clump cap.
/// </summary>
public class MonsterTribeGuardRecipeTests
{
    /// <summary>
    ///     A manually-spawned Tribe Guard, already in <see cref="MonsterAiState.Decision" />. <c>Type = 6</c> is
    ///     one of the four Tribe-Guard Type codes recovered this session -- <see cref="MonsterEntity.Create" />'s
    ///     default derivation (no explicit <c>specialSort</c> override) already resolves this to
    ///     <see cref="MonsterSpecialSort.TribeGuard" /> via <see cref="MonsterSpecialSort.Derive" />, so these
    ///     tests exercise the Derive fix and the recipe body together, end-to-end, not just the recipe alone.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster) CreateGuardZone(short meleeRadius, IRandomSource ai)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Type = 6,
            Life = 100_000,
            RadiusInfo1 = meleeRadius,
            FollowInfo1 = 5,
            FollowInfo2 = 5
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        Assert.Equal(MonsterSpecialSort.TribeGuard, monster.SpecialSort);
        monster.AiState = MonsterAiState.Decision;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(ai)]);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static void EnterPlayerAt(Zone zone, int characterId, float posX, float posZ)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, $"P{characterId}", posX, 0f, posZ)));
    }

    [Fact]
    public void CandidateWithinMeleeRange_EngagesImmediately_WithNoIntermediateChaseState()
    {
        var (zone, _) = CreateGuardZone(meleeRadius: 50, ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 20, posZ: 0);

        // Tick 1 drains the Enter -- since the monster is already in Decision, this is also its own first
        // Decision dispatch (DetectionThrottleTicks 0->1, blocked). Tick 2 crosses the throttle (1->2), the
        // scan runs and finds candidate 10 already within melee range -> direct engagement, same tick.
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState); // never Chase -- a guard never pursues
        Assert.Equal(10, live.TargetCharacterId);
    }

    [Fact]
    public void CandidateBeyondMeleeRange_NeverEngages_NoChaseCapabilityAtAll()
    {
        // Unlike the Standard recipe's own post-prune engagement (which falls back to a chase toward an
        // arc-offset approach point), the guard recipe has no fallback at all for a beyond-range candidate --
        // it simply never engages, no matter how long it keeps scanning.
        var (zone, _) = CreateGuardZone(meleeRadius: 5, ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 500, posZ: 0);

        for (var i = 0; i < 10; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void DeadCandidate_IsExcludedByTheSharedGauntlet_NeverEngages()
    {
        var (zone, _) = CreateGuardZone(meleeRadius: 50, ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 20, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick); // drains the Enter (also throttle 0->1, blocked)

        Assert.True(zone.TryGetPlayer(10, out var candidate));
        candidate!.IsDead = true;

        zone.Tick(SimulationClock.LegacyTick); // throttle elapsed -> scan runs, but the sole candidate is dead

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void NoAntiClumpCap_MultipleGuardsMayTargetTheSameCandidateSimultaneously()
    {
        // Confirmed by absence (the recipe's own remarks): unlike TryAcquireTarget's CountOtherPursuers check,
        // the guard recipe has no pursuer-capacity cap of any kind -- two guards may both lock the SAME
        // candidate in the same tick, with no cross-guard limit.
        var template = WorldDataTestRows.Monster(600) with
        {
            Type = 6, Life = 100_000, RadiusInfo1 = 50, FollowInfo1 = 1, FollowInfo2 = 1
        };
        var guardA = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        var guardB = MonsterEntity.Create(2, 2, template, 2, 0, 0, 0, 50);
        guardA.AiState = MonsterAiState.Decision;
        guardB.AiState = MonsterAiState.Decision;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options,
            simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))]);
        zone.SpawnMonster(guardA);
        zone.SpawnMonster(guardB);
        EnterPlayerAt(zone, 10, posX: 20, posZ: 0);

        zone.Tick(SimulationClock.LegacyTick); // drains the Enter -- also both guards' first Decision tick
        zone.Tick(SimulationClock.LegacyTick); // throttle elapsed -> both guards independently acquire #10

        Assert.True(zone.TryGetMonster(1, out var liveA));
        Assert.True(zone.TryGetMonster(2, out var liveB));
        Assert.Equal(10, liveA!.TargetCharacterId);
        Assert.Equal(10, liveB!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, liveA.AiState);
        Assert.Equal(MonsterAiState.AttackWindup, liveB.AiState);
    }
}
