using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemMidChaseRetargetTests
{
    private const int NearCharacterId = 10;
    private const int FarCharacterId = 11;

    [Fact]
    public void HeightMismatchedCandidate_WithinHorizontalShortRadius_IsNeverRetargetedOnto()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5,
            new ScriptedRandomSource(0));
        EnterPlayerAt(zone, NearCharacterId, 500, 0, 0);
        EnterPlayerAt(zone, FarCharacterId, 50, 1000, 0);
        LockInitialTarget(monster, NearCharacterId, 500, 0);

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
        var (zone, monster) = CreateChaseZone(200, 2000, 5,
            new ScriptedRandomSource(0));
        EnterPlayerAt(zone, NearCharacterId, 500, 0, 0);
        EnterPlayerAt(zone, FarCharacterId, 50, 0, 0);
        LockInitialTarget(monster, NearCharacterId, 500, 0);

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
        var (zone, monster) = CreateChaseZone(200, 2000, 5,
            new ScriptedRandomSource(0));
        EnterPlayerAt(zone, NearCharacterId, 500, 0, 0);
        EnterPlayerAt(zone, FarCharacterId, 50, 0, 0);
        LockInitialTarget(monster, NearCharacterId, 500, 0);

        zone.Tick(TimeSpan.FromMilliseconds(1000));

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(FarCharacterId, live!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, live.AiState);
    }

    [Fact]
    public void FailedRollOnOneWindow_DoesNotBlockASuccessfulRollOnTheNextWindow()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5,
            new ScriptedRandomSource(1, 0));
        EnterPlayerAt(zone, NearCharacterId, 500, 0, 0);
        EnterPlayerAt(zone, FarCharacterId, 50, 0, 0);
        LockInitialTarget(monster, NearCharacterId, 500, 0);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var afterMiss));
        Assert.Equal(NearCharacterId, afterMiss!.TargetCharacterId);
        Assert.Equal(MonsterAiState.Chase, afterMiss.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var afterHit));
        Assert.Equal(FarCharacterId, afterHit!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, afterHit.AiState);
    }

    [Fact]
    public void AttackTypeOutsideEligibleSet_NeverRetargets_EvenWithAPerfectCandidate()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5,
            new ScriptedRandomSource(0), 2);
        EnterPlayerAt(zone, NearCharacterId, 500, 0, 0);
        EnterPlayerAt(zone, FarCharacterId, 5, 0, 0);
        LockInitialTarget(monster, NearCharacterId, 500, 0);

        for (var i = 0; i < 8; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            Assert.Equal(NearCharacterId, live!.TargetCharacterId);
            Assert.Equal(MonsterAiState.Chase, live.AiState);
        }
    }


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
