using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemChaseHeightGateTests
{
    private const int TargetCharacterId = 10;

    [Fact]
    public void TargetWithinMeleeRangeButBeyondHeightTolerance_RoutesToReturnToSpawn_NotAttackWindup()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5);
        EnterPlayerAt(zone, TargetCharacterId, 50, 100, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
    }

    [Fact]
    public void TargetWithinMeleeRangeAndHeightTolerance_CommitsToAttackWindupAndFacesTarget()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5);
        EnterPlayerAt(zone, TargetCharacterId, 50, 3, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);

        var expectedHeading = MathF.Atan2(50f - live.PosX, 0f - live.PosZ);
        Assert.True(MathF.Abs(live.Heading - expectedHeading) < 0.0001f);
    }

    [Fact]
    public void VerticalSeparationExactlyAtTemplateTolerance_StillCountsAsAttackable()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5);
        EnterPlayerAt(zone, TargetCharacterId, 50, 5, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void TargetWithinMeleeRangeAndHeightTolerance_QualifyingAttackType_ArmsAttackPacketConfirmation(
        byte attackType)
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5, attackType);
        EnterPlayerAt(zone, TargetCharacterId, 50, 3, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);
        Assert.True(live.AttackPacketConfirmationArmed);
    }

    [Fact]
    public void TargetWithinMeleeRangeAndHeightTolerance_NonQualifyingAttackType_LeavesConfirmationUnarmed()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5, attackType: 3);
        EnterPlayerAt(zone, TargetCharacterId, 50, 3, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);
        Assert.False(live.AttackPacketConfirmationArmed);
    }

    [Fact]
    public void TargetWithinMeleeRangeButBeyondHeightTolerance_NeverArmsAttackPacketConfirmation()
    {
        var (zone, monster) = CreateChaseZone(200, 2000, 5);
        EnterPlayerAt(zone, TargetCharacterId, 50, 100, 0);
        LockInitialTarget(monster, TargetCharacterId, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
        Assert.False(live.AttackPacketConfirmationArmed);
    }

    private static (Zone Zone, MonsterEntity Monster) CreateChaseZone(short meleeRadius, short detectionRadius,
        short heightTolerance, byte attackType = 1)
    {
        var template = WorldDataTestRows.Monster(701) with
        {
            Life = 100_000,
            AttackType = attackType,
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = detectionRadius,
            Size2 = heightTolerance,
            WalkSpeed = 0,
            RunSpeed = 0,
            FrameInfo1 = 1,
            FrameInfo3 = 2
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0);
        monster.AiState = MonsterAiState.Chase;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))]);
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
