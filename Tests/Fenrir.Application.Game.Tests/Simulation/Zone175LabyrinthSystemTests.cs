using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Simulation;

public sealed class Zone175LabyrinthSystemTests
{
    private const short LabyrinthMapId = 175;
    private const int OneMinute = Zone175RewardTables.OneMinuteLegacyTicks;

    private static DateTimeOffset NextSunday2100()
    {
        var candidate = new DateTimeOffset(2026, 7, 12, 21, 0, 0, TimeSpan.Zero);
        while (candidate.DayOfWeek != DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    private static Zone175LabyrinthConfig EnabledConfig(int index2 = 4)
    {
        return new Zone175LabyrinthConfig(new Dictionary<short, Zone175InstanceConfig>
        {
            [LabyrinthMapId] = new(0, index2, 1f, 1f)
        });
    }

    private static void SpawnMonster(Zone zone, int serverIndex, byte specialType)
    {
        var template = WorldDataTestRows.Monster(600 + serverIndex) with { SpecialType = specialType };
        zone.SpawnMonster(MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
            100f, 0f, 100f, 50f));
    }

    private static PlayerRuntimeState EnterPlayer(Zone zone, int characterId)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    [Fact]
    public void CountLivingZone175WaveBosses_CountsOnlyTheGivenSpecialType()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        SpawnMonster(zone, 500, 40);
        SpawnMonster(zone, 501, 40);
        SpawnMonster(zone, 502, 5);

        Assert.Equal(2, zone.CountLivingZone175WaveBosses(40));
        Assert.Equal(0, zone.CountLivingZone175WaveBosses(41));
    }

    [Fact]
    public void RemoveZone175MissionMonsters_RemovesWaveBossesOnly()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        SpawnMonster(zone, 500, 40);
        SpawnMonster(zone, 501, 44);
        SpawnMonster(zone, 502, 5);

        zone.RemoveZone175MissionMonsters();

        Assert.False(zone.TryGetMonster(500, out _));
        Assert.False(zone.TryGetMonster(501, out _));
        Assert.True(zone.TryGetMonster(502, out _));
    }

    [Fact]
    public void HasAnyZone175QualifyingPlayer_ReflectsPresenceRule()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var state = EnterPlayer(zone, 10);
        Assert.True(zone.HasAnyZone175QualifyingPlayer());

        state.VisibleState = 0;
        Assert.False(zone.HasAnyZone175QualifyingPlayer());

        state.VisibleState = 1;
        state.IsMovingZone = true;
        Assert.False(zone.HasAnyZone175QualifyingPlayer());

        state.IsMovingZone = false;
        state.IsDead = true;
        Assert.True(zone.HasAnyZone175QualifyingPlayer());
    }

    [Fact]
    public void ForceDisconnectAllForZone175_AbortsEverySessionWithTheMissionsOwnDisconnectReason()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(10, out _));

        zone.ForceDisconnectAllForZone175();

        Assert.Equal(DisconnectReason.LabyrinthMissionEnded, session.DisconnectReason);
    }

    [Fact]
    public void GrantZone175WaveReward_GrantsFixedMoneyAndCp_AndResetsBossDamage_ForEligiblePlayer()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var state = EnterPlayer(zone, 10);
        state.RebirthCount = 3;
        state.ContributionPoints = 0;
        state.Zone175BossDamage = 123;

        zone.GrantZone175WaveReward(1, 1f);

        Assert.Equal(20, state.ContributionPoints);
        Assert.Equal(0, state.Zone175BossDamage);
        var grant = Assert.Single(zone.DrainPendingMoneyGrants());
        Assert.Equal(10, grant.CharacterId);
        Assert.Equal(100_000_000L, grant.Amount);
    }

    [Fact]
    public void GrantZone175WaveReward_Stage5_Grants200MAnd200Cp()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var state = EnterPlayer(zone, 10);
        state.ContributionPoints = 0;

        zone.GrantZone175WaveReward(5, 1f);

        Assert.Equal(200, state.ContributionPoints);
        var grant = Assert.Single(zone.DrainPendingMoneyGrants());
        Assert.Equal(200_000_000L, grant.Amount);
    }

    [Fact]
    public void GrantZone175WaveReward_SkipsDeadPlayer()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var state = EnterPlayer(zone, 10);
        state.ContributionPoints = 7;
        state.Zone175BossDamage = 55;
        state.IsDead = true;

        zone.GrantZone175WaveReward(3, 1f);

        Assert.Equal(7, state.ContributionPoints);
        Assert.Equal(55, state.Zone175BossDamage);
        Assert.Empty(zone.DrainPendingMoneyGrants());
    }

    [Fact]
    public void System_DisabledConfig_IsANoOp()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var time = new MutableTimeProvider(NextSunday2100());
        var system = new Zone175LabyrinthSystem(Zone175LabyrinthConfig.Disabled,
            NullLogger<Zone175LabyrinthSystem>.Instance, time);

        system.Simulate(zone, 1);

        Assert.False(system.TryGetPhase(LabyrinthMapId, out _));
    }

    [Fact]
    public void System_ConfiguredMap_OffSchedule_StaysIdle()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var time = new MutableTimeProvider(NextSunday2100().AddDays(1));
        var system = new Zone175LabyrinthSystem(EnabledConfig(), NullLogger<Zone175LabyrinthSystem>.Instance, time);

        system.Simulate(zone, 1);

        Assert.True(system.TryGetPhase(LabyrinthMapId, out var phase));
        Assert.Equal(Zone175MissionPhase.Idle, phase);
    }

    [Fact]
    public void System_EmptyZone_OpensCountsDownSummonsThenEmptyAbortsToTerminalAndResets()
    {
        var zone = ZoneTestKit.CreateZone(LabyrinthMapId);
        var time = new MutableTimeProvider(NextSunday2100());
        var system = new Zone175LabyrinthSystem(EnabledConfig(), NullLogger<Zone175LabyrinthSystem>.Instance, time);

        system.Simulate(zone, 1);
        Assert.True(system.TryGetPhase(LabyrinthMapId, out var afterOpen));
        Assert.Equal(Zone175MissionPhase.PreOpen, afterOpen);

        system.Simulate(zone, 10 * OneMinute);
        system.Simulate(zone, 1);
        system.Simulate(zone, 1);

        Assert.True(system.TryGetPhase(LabyrinthMapId, out var afterAbort));
        Assert.Equal(Zone175MissionPhase.Terminal, afterAbort);

        system.Simulate(zone, Zone175RewardTables.TerminalHoldLegacyTicks);

        Assert.True(system.TryGetPhase(LabyrinthMapId, out var afterReset));
        Assert.Equal(Zone175MissionPhase.Idle, afterReset);
    }

        private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
