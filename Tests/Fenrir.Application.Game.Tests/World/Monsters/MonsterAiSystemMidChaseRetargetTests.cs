using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers workstream A3's <c>A3-mid-chase-retarget</c> behavior contract:
///     <see cref="MonsterAiSystem" />'s private <c>TryShortRangeRetarget</c> (legacy
///     <c>SelectAvatarIndexForAttackAction</c> / <c>SelectAvatarIndexForShortRange</c>,
///     <c>S07_MyGame05.cpp:518-594</c>), observed only through <see cref="Zone" />'s public surface.
/// </summary>
/// <remarks>
///     Every fixture here bypasses <see cref="MonsterSpawnScheduler" />/the wide-radius acquisition path
///     entirely: the monster is constructed already in <see cref="MonsterAiState.Chase" /> with a
///     directly-assigned locked target (<see cref="MonsterEntity.AssignTarget" />), exactly the "manual
///     monster + AI" idiom <c>MonsterAiSystemRecipesTests</c> already established. This is required to
///     isolate the SHORT-range re-target scan on its own: <c>RadiusInfo1</c> is, by Fenrir's own design,
///     simultaneously the re-target scan's own acceptance radius AND <see cref="Zone" />'s outer
///     attack-transition threshold a tick later in the SAME <c>RunChase</c> call -- so the pre-existing
///     "current" locked target is always positioned OUTSIDE that radius in every fixture below (matching
///     the only state actually reachable through normal play: a target already inside melee range would
///     have already transitioned <see cref="MonsterAiState" /> out of <see cref="MonsterAiState.Chase" />
///     via that same outer check before the short-range scan could ever matter).
///     <para>
///         NOT independently tested: the contract's "no exclusion of the current target from candidacy"
///         edge case. Given the geometry constraint above (current always sits outside the short radius
///         while in reach of this mechanism), legally re-accepting "current" is observationally identical
///         to leaving it unchanged either with or without an exclusion check -- the contract itself frames
///         this as "a behavioral no-op," and no reachable fixture makes it otherwise.
///     </para>
/// </remarks>
public class MonsterAiSystemMidChaseRetargetTests
{
    private const int NearCharacterId = 10;
    private const int FarCharacterId = 11;

    [Fact]
    public void HeightMismatchedCandidate_WithinHorizontalShortRadius_IsNeverRetargetedOnto()
    {
        // Body-height half-extent gate (Server/Header/Protocol/STRUCT.h:165, mSize[1] -> MonsterRowDto.Size2):
        // a candidate can sit well inside the horizontal short radius and still never be accepted if the
        // vertical gap exceeds the species' own half-extent. "Near" (the pre-existing locked target) sits
        // OUTSIDE the short radius the whole test so it can never itself trip RunChase's own independent
        // attack-transition check -- see class remarks.
        var (zone, monster) = CreateChaseZone(shortRadius: 200, largeRadius: 2000, bodyHeightHalfExtent: 5,
            ai: new ScriptedRandomSource(0)); // every 50% roll would succeed if ever reached
        EnterPlayerAt(zone, NearCharacterId, posX: 500, posY: 0, posZ: 0);
        EnterPlayerAt(zone, FarCharacterId, posX: 50, posY: 1000, posZ: 0); // horizontally close, vertically far
        LockInitialTarget(monster, NearCharacterId, posX: 500, posZ: 0);

        for (var i = 0; i < 8; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            Assert.Equal(NearCharacterId, live!.TargetCharacterId);
            Assert.Equal(MonsterAiState.Chase, live.AiState);
        }
    }

    [Fact]
    public void HeightMatchedCandidate_WithinShortRadius_GetsRetargetedOnto_ThenForcesAttackWait()
    {
        // Positive control for the previous test: identical geometry, except the candidate's height now
        // matches the monster's own -- it passes the vertical gate, the horizontal short-radius test, and
        // the (always-succeeding, scripted) 50% roll, so it IS retargeted onto. The retarget itself never
        // transitions state; RunChase's own subsequent attack-radius check does, immediately afterward in
        // the same tick, because the short radius backs both tests (contract's Side Effects walk-through).
        var (zone, monster) = CreateChaseZone(shortRadius: 200, largeRadius: 2000, bodyHeightHalfExtent: 5,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, NearCharacterId, posX: 500, posY: 0, posZ: 0);
        EnterPlayerAt(zone, FarCharacterId, posX: 50, posY: 0, posZ: 0); // now height-eligible too
        LockInitialTarget(monster, NearCharacterId, posX: 500, posZ: 0);

        var retargeted = false;
        for (var i = 0; i < 8 && !retargeted; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            if (live!.TargetCharacterId == FarCharacterId)
                retargeted = true;
        }

        Assert.True(retargeted, "monster never re-targeted onto the height-and-radius-eligible candidate");
        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var final));
        Assert.Equal(MonsterAiState.AttackWindup, final!.AiState);
    }

    [Fact]
    public void ReselectThrottle_AccruesInRealElapsedTime_NotOncePerSimulateCall()
    {
        // A3-mid-chase-retarget contract: the ~1 s reselect throttle is REAL elapsed time (2 legacy ticks),
        // not a flat per-Simulate-call counter. A single Zone.Tick fed a full 1000 ms (2 whole legacy ticks
        // in ONE Simulate pass -- e.g. a stalled host's catch-up frame) must cross the threshold and fire the
        // scan within that SAME call. A per-call-only counter would need a SECOND separate Tick call instead.
        var (zone, monster) = CreateChaseZone(shortRadius: 200, largeRadius: 2000, bodyHeightHalfExtent: 5,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, NearCharacterId, posX: 500, posY: 0, posZ: 0);
        EnterPlayerAt(zone, FarCharacterId, posX: 50, posY: 0, posZ: 0);
        LockInitialTarget(monster, NearCharacterId, posX: 500, posZ: 0);

        zone.Tick(TimeSpan.FromMilliseconds(1000)); // exactly SimulationClock.MonsterDetectionThrottleLegacyTicks worth

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(FarCharacterId, live!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, live.AiState);
    }

    [Fact]
    public void FailedRollOnOneWindow_DoesNotBlockASuccessfulRollOnTheNextWindow()
    {
        // Probabilistic acceptance (S07_MyGame05.cpp:586-591): a lost 50% flip only skips the candidate for
        // the REST of that one scan -- it is retried fresh on the next throttle window, not permanently
        // blocked, and the throttle itself still resets on a miss (the same reset-on-every-attempt posture
        // as the wide-detect scan). ScriptedRandomSource(1, 0): the first roll (1 % 2 == 1) fails, the
        // second (0 % 2 == 0) succeeds.
        var (zone, monster) = CreateChaseZone(shortRadius: 200, largeRadius: 2000, bodyHeightHalfExtent: 5,
            ai: new ScriptedRandomSource(1, 0));
        EnterPlayerAt(zone, NearCharacterId, posX: 500, posY: 0, posZ: 0);
        EnterPlayerAt(zone, FarCharacterId, posX: 50, posY: 0, posZ: 0);
        LockInitialTarget(monster, NearCharacterId, posX: 500, posZ: 0);

        // First throttle window (2 legacy ticks): the scan runs and rolls, but the roll fails -- target
        // stays unchanged, monster stays in Chase (Near never enters its own attack range in this fixture).
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var afterMiss));
        Assert.Equal(NearCharacterId, afterMiss!.TargetCharacterId);
        Assert.Equal(MonsterAiState.Chase, afterMiss.AiState);

        // Second throttle window: a fresh attempt, fresh roll -- this one succeeds.
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var afterHit));
        Assert.Equal(FarCharacterId, afterHit!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, afterHit.AiState);
    }

    [Fact]
    public void AttackTypeOutsideEligibleSet_NeverRetargets_EvenWithAPerfectCandidate()
    {
        // Preconditions: the mechanism is a guaranteed no-op outside attack-type {1,3,6} -- AttackType=2 is a
        // real, common legacy value that must never trigger a reselect, even with an otherwise flawless
        // candidate sitting right on top of the monster.
        var (zone, monster) = CreateChaseZone(shortRadius: 200, largeRadius: 2000, bodyHeightHalfExtent: 5,
            ai: new ScriptedRandomSource(0), attackType: 2);
        EnterPlayerAt(zone, NearCharacterId, posX: 500, posY: 0, posZ: 0);
        EnterPlayerAt(zone, FarCharacterId, posX: 5, posY: 0, posZ: 0);
        LockInitialTarget(monster, NearCharacterId, posX: 500, posZ: 0);

        for (var i = 0; i < 8; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            Assert.Equal(NearCharacterId, live!.TargetCharacterId);
            Assert.Equal(MonsterAiState.Chase, live.AiState);
        }
    }

    // ---- fixtures ------------------------------------------------------------------------------------------

    /// <summary>
    ///     A zone with just the AI system (no spawn scheduler) and one manually-spawned, manually-parked
    ///     monster -- <c>RunSpeed</c>/<c>WalkSpeed</c> pinned to 0 so no incidental movement can ever close
    ///     the deliberately-large test distances during the loop, isolating the retarget scan's own
    ///     decision from ordinary chase movement.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster) CreateChaseZone(short shortRadius, short largeRadius,
        short bodyHeightHalfExtent, IRandomSource ai, byte attackType = 1)
    {
        var template = WorldDataTestRows.Monster(700) with
        {
            Life = 100_000,
            AttackType = attackType,
            RadiusInfo1 = shortRadius,
            RadiusInfo2 = largeRadius,
            Size2 = bodyHeightHalfExtent,
            WalkSpeed = 0,
            RunSpeed = 0,
            FrameInfo1 = 1,
            FrameInfo3 = 2
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        monster.AiState = MonsterAiState.Chase;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(ai)]);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static void LockInitialTarget(MonsterEntity monster, int characterId, float posX, float posZ)
    {
        monster.AssignTarget(characterId, 0, posX, 0f, posZ);
    }

    private static void EnterPlayerAt(Zone zone, int characterId, float posX, float posY, float posZ)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, $"P{characterId}", posX, posY, posZ)));
    }
}
