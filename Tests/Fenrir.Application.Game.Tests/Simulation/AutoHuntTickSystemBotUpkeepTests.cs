using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Simulation;

public class AutoHuntTickSystemBotUpkeepTests
{
    private const short Zone126MapId = 126;
    private const short OrdinaryMapId = 1;

    private static (Zone Zone, PlayerRuntimeState State, ZoneClientSession Session, FakeDuplexPipe Pipe,
        AutoHuntTickSystem System) SetUp(short mapId, short manaUse)
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(manaUse) }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var dirtyTracker = new DirtyTracker<int>();
        var opts = ZoneTestKit.Options();
        opts.Zone126TypeMapIds.Add(Zone126MapId);
        var system = new AutoHuntTickSystem(worldData, dirtyTracker, Options.Create(opts));

        var zone = ZoneTestKit.CreateZone(mapId, opts, dirtyTracker, [], worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        ZoneTestKit.DrainOutbound(pipe);

        state!.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        return (zone, state, session, pipe, system);
    }

    private static AutoHunt Config(params int[] buffStorePairs)
    {
        var buffStore = new int[16];
        Array.Copy(buffStorePairs, buffStore, buffStorePairs.Length);
        return new AutoHunt
        {
            BuffType = 0, BuffStore = buffStore, HuntType = 0, AttackType = new int[4],
            MonNum = 0, ItemType = 0, InvenCmd = 0, DeathCmd = 0, AnimalPreyCmd = 0, AnimalFoodCmd = 0
        };
    }

    private static SkillDefinition HolyShieldSkill(short manaUse)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, 10, 1, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty,
            [HolyShieldGrade(0, manaUse), HolyShieldGrade(1, manaUse)]);
    }

    private static SkillGradeRowDto HolyShieldGrade(byte gradeIndex, short manaUse)
    {
        return new SkillGradeRowDto(82, gradeIndex, manaUse, 0,
            0, 0, 0, 0, 0, 0,
            0, 40, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 20, 0, 0, 0,
            0, 0);
    }

    private static byte[] ReturnToAutoZoneFrame()
    {
        var frame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), frame);
        return frame;
    }


    [Fact]
    public void Zone126_SelectedBuffCannotAffordMana_IncrementsNoManaCounter()
    {
        var (zone, state, _, _, system) = SetUp(Zone126MapId, 9999);

        system.Simulate(zone, 1);
        Assert.Equal(1, state.NoManaCount);

        system.Simulate(zone, 1);
        Assert.Equal(2, state.NoManaCount);
    }

    [Fact]
    public void Zone126_NoManaCounterReachesExactly1000_RelocatesToAutoZone_NoDisconnect()
    {
        var (zone, state, session, pipe, system) = SetUp(Zone126MapId, 9999);
        state.NoManaCount = 999;

        system.Simulate(zone, 1);

        Assert.Equal(1000, state.NoManaCount);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void Zone126_NoManaCounterExceeds1000_DisconnectsSession()
    {
        var (zone, state, session, _, system) = SetUp(Zone126MapId, 9999);
        state.NoManaCount = 1000;

        system.Simulate(zone, 1);

        Assert.Equal(1001, state.NoManaCount);
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void ProceedingCast_ResetsNoManaCounter()
    {
        var (zone, state, _, _, system) = SetUp(Zone126MapId, 30);
        state.NoManaCount = 500;
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
        Assert.Equal(manaBefore - 30, state.Mana);
    }

    [Fact]
    public void NonZone126_InsufficientMana_NeverEscalates()
    {
        var (zone, state, session, pipe, system) = SetUp(OrdinaryMapId, 9999);

        for (var i = 0; i < 5; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Null(session.DisconnectReason);
    }


    [Fact]
    public void Budget_BothTiersZero_IsInert_BuffStillCasts()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, 30);
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Budget_PresentAndValidDayTier_DoesNotRelocate_BuffStillCasts()
    {
        var (zone, state, _, _, system) = SetUp(OrdinaryMapId, 30);
        state.AutoHuntPaidDayBudget = 99_991_231;
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(99_991_231, state.AutoHuntPaidDayBudget);
        Assert.Equal(manaBefore - 30, state.Mana);
    }

    [Fact]
    public void Budget_DayTierExpiredWithNoMinute_RelocatesAndAbandonsBuff()
    {
        var (zone, state, session, pipe, system) = SetUp(OrdinaryMapId, 30);
        state.AutoHuntPaidDayBudget = 20_000_101;
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AutoHuntPaidDayBudget);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2 + 1]);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void Budget_MinuteTierHitsZero_RelocatesAndAbandonsBuff()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, 30);
        state.AutoHuntPaidMinuteBudget = 1;
        state.AutoHuntBudgetMinuteAccrualTicks = SimulationClock.PlayTimeAccrualLegacyTicks - 1;
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AutoHuntPaidMinuteBudget);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(manaBefore, state.Mana);
    }

    [Fact]
    public void Budget_MinuteTierWithTimeLeft_DecrementsButDoesNotRelocate()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, 30);
        state.AutoHuntPaidMinuteBudget = 5;
        state.AutoHuntBudgetMinuteAccrualTicks = SimulationClock.PlayTimeAccrualLegacyTicks - 1;

        system.Simulate(zone, 1);

        Assert.Equal(4, state.AutoHuntPaidMinuteBudget);
        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0);
    }

    [Fact]
    public void Budget_DecrementRunsEvenWhenBuffGateWouldReject_Stunned()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, 30);
        state.IsStunned = true;
        state.AutoHuntPaidDayBudget = 20_000_101;

        system.Simulate(zone, 1);

        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
    }
}
