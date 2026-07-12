using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterTribeGuardRecipeTests
{
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
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0);
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
        var (zone, _) = CreateGuardZone(50, new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, 20, 0);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);
        Assert.Equal(10, live.TargetCharacterId);
    }

    [Fact]
    public void CandidateBeyondMeleeRange_NeverEngages_NoChaseCapabilityAtAll()
    {
        var (zone, _) = CreateGuardZone(5, new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, 500, 0);

        for (var i = 0; i < 10; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void DeadCandidate_IsExcludedByTheSharedGauntlet_NeverEngages()
    {
        var (zone, _) = CreateGuardZone(50, new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, 20, 0);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(10, out var candidate));
        candidate!.IsDead = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void NoAntiClumpCap_MultipleGuardsMayTargetTheSameCandidateSimultaneously()
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Type = 6, Life = 100_000, RadiusInfo1 = 50, FollowInfo1 = 1, FollowInfo2 = 1
        };
        var guardA = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0);
        var guardB = MonsterEntity.Create(2, 2, template, 2, 0, 0, 0);
        guardA.AiState = MonsterAiState.Decision;
        guardB.AiState = MonsterAiState.Decision;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options,
            simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))]);
        zone.SpawnMonster(guardA);
        zone.SpawnMonster(guardB);
        EnterPlayerAt(zone, 10, 20, 0);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var liveA));
        Assert.True(zone.TryGetMonster(2, out var liveB));
        Assert.Equal(10, liveA!.TargetCharacterId);
        Assert.Equal(10, liveB!.TargetCharacterId);
        Assert.Equal(MonsterAiState.AttackWindup, liveA.AiState);
        Assert.Equal(MonsterAiState.AttackWindup, liveB.AiState);
    }
}
