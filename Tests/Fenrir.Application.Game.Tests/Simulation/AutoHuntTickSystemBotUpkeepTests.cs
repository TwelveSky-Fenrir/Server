using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
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

/// <summary>
///     Covers the B11 additions to <see cref="AutoHuntTickSystem" />: the zone-126-type no-mana escalation
///     (<c>S07_MyGame04.cpp:2469-2485</c>) and the two-tier paid auto-hunt-time budget + return-to-auto-zone
///     (<c>:787-824</c>). Each test drives <see cref="AutoHuntTickSystem.Simulate" /> directly (the zone's own
///     pipeline does not run the system) so the only outbound bytes are the ones this system produces. The
///     zone-126-type flag itself is now config-driven (<see cref="Zone.IsZone126TypeZone" />), so every
///     <c>Zone126MapId</c>-based test here populates <see cref="GameServerOptions.Zone126TypeMapIds" /> --
///     see the sibling <c>AutoHuntTickSystemZone126ConfigTests</c> for coverage of that config wiring itself.
/// </summary>
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
        // Zone.IsZone126TypeZone is config-driven (GameServerOptions.Zone126TypeMapIds, empty by default) --
        // populate the operator-configured map-id set the same way a real deployment would classify map 126 as
        // "zone-126-type" (the legacy server-number-210-218 equivalent), matching AutoHuntTickSystem's own
        // corrected zone-126 flag resolution. Map id 1 (OrdinaryMapId) is never in this set, so it stays
        // unclassified regardless.
        opts.Zone126TypeMapIds.Add(Zone126MapId);
        var system = new AutoHuntTickSystem(worldData, dirtyTracker, Options.Create(opts));

        // Empty simulation set: the system is driven directly, never through Zone.Tick's pipeline.
        var zone = ZoneTestKit.CreateZone(mapId, opts, dirtyTracker, [], worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drains Enter; too short to run any system

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

    // Holy Shield (82): no weapon requirement, slot 9, 20% of MaxLife, 40-tick duration.
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

    // ---- No-mana escalation (zone-126-type only) --------------------------------------------------------

    [Fact]
    public void Zone126_SelectedBuffCannotAffordMana_IncrementsNoManaCounter()
    {
        var (zone, state, _, _, system) = SetUp(Zone126MapId, manaUse: 9999); // 9999 > default mana 300

        system.Simulate(zone, 1);
        Assert.Equal(1, state.NoManaCount);

        system.Simulate(zone, 1);
        Assert.Equal(2, state.NoManaCount);
    }

    [Fact]
    public void Zone126_NoManaCounterReachesExactly1000_RelocatesToAutoZone_NoDisconnect()
    {
        var (zone, state, session, pipe, system) = SetUp(Zone126MapId, manaUse: 9999);
        state.NoManaCount = 999;

        system.Simulate(zone, 1);

        Assert.Equal(1000, state.NoManaCount);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void Zone126_NoManaCounterExceeds1000_DisconnectsSession()
    {
        var (zone, state, session, _, system) = SetUp(Zone126MapId, manaUse: 9999);
        state.NoManaCount = 1000;

        system.Simulate(zone, 1);

        Assert.Equal(1001, state.NoManaCount);
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void ProceedingCast_ResetsNoManaCounter()
    {
        var (zone, state, _, _, system) = SetUp(Zone126MapId, manaUse: 30); // affordable
        state.NoManaCount = 500;
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
        Assert.Equal(manaBefore - 30, state.Mana);
    }

    [Fact]
    public void NonZone126_InsufficientMana_NeverEscalates()
    {
        var (zone, state, session, pipe, system) = SetUp(OrdinaryMapId, manaUse: 9999);

        for (var i = 0; i < 5; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Null(session.DisconnectReason);
    }

    // ---- Two-tier paid-time budget + return-to-auto-zone ------------------------------------------------

    [Fact]
    public void Budget_BothTiersZero_IsInert_BuffStillCasts()
    {
        // The present-based gate: a player with no purchased time is never bounced, and the ordinary buff loop
        // still runs (the shipped auto-hunt buff feature must not be self-DoS'd -- see AutoHuntBudgetPolicy).
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, manaUse: 30);
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(manaBefore - 30, state.Mana); // buff cast
        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // no relocate
    }

    [Fact]
    public void Budget_PresentAndValidDayTier_DoesNotRelocate_BuffStillCasts()
    {
        var (zone, state, _, _, system) = SetUp(OrdinaryMapId, manaUse: 30);
        state.AutoHuntPaidDayBudget = 99_991_231; // far-future expiry -> valid
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(99_991_231, state.AutoHuntPaidDayBudget);
        Assert.Equal(manaBefore - 30, state.Mana); // buff still cast
    }

    [Fact]
    public void Budget_DayTierExpiredWithNoMinute_RelocatesAndAbandonsBuff()
    {
        var (zone, state, session, pipe, system) = SetUp(OrdinaryMapId, manaUse: 30);
        state.AutoHuntPaidDayBudget = 20_000_101; // already in the past
        var manaBefore = state.Mana;

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AutoHuntPaidDayBudget);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe)); // relocated
        Assert.Equal(manaBefore, state.Mana); // buff abandoned this tick
        Assert.Equal(0, state.Buffs.Buff[9 * 2 + 1]);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void Budget_MinuteTierHitsZero_RelocatesAndAbandonsBuff()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, manaUse: 30);
        state.AutoHuntPaidMinuteBudget = 1;
        state.AutoHuntBudgetMinuteAccrualTicks = SimulationClock.PlayTimeAccrualLegacyTicks - 1; // one tick shy of a minute
        var manaBefore = state.Mana;

        system.Simulate(zone, 1); // completes the minute -> minute 1 -> 0 -> exhausted

        Assert.Equal(0, state.AutoHuntPaidMinuteBudget);
        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(manaBefore, state.Mana); // buff abandoned
    }

    [Fact]
    public void Budget_MinuteTierWithTimeLeft_DecrementsButDoesNotRelocate()
    {
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, manaUse: 30);
        state.AutoHuntPaidMinuteBudget = 5;
        state.AutoHuntBudgetMinuteAccrualTicks = SimulationClock.PlayTimeAccrualLegacyTicks - 1;

        system.Simulate(zone, 1); // one whole minute completes

        Assert.Equal(4, state.AutoHuntPaidMinuteBudget);
        // Not exhausted -> no relocate; the buff loop still runs after the decrement.
        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0);
    }

    [Fact]
    public void Budget_DecrementRunsEvenWhenBuffGateWouldReject_Stunned()
    {
        // Group D is gated only on the auto-hunt flag, NOT the buff/hotkey death/stun gate: a stunned player's
        // paid time still drains and still relocates on exhaustion.
        var (zone, state, _, pipe, system) = SetUp(OrdinaryMapId, manaUse: 30);
        state.IsStunned = true;
        state.AutoHuntPaidDayBudget = 20_000_101; // expired

        system.Simulate(zone, 1);

        Assert.Equal(ReturnToAutoZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
    }
}
