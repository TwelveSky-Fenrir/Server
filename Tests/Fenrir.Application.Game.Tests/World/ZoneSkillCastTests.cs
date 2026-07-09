using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplySkillCastManaCharge</c> (op15 CZ_AVATAR_ACTION_SEND cast-start, action-category
///     Sort resolving to the real skill-cast category -- NOT Sort=30, the unrelated stand-up-from-death
///     request) and <c>Zone.ApplySkillEffectConfirm</c> (op16 CZ_UPDATE_AVATAR_ACTION Sort==1 confirm)
///     end-to-end, via the same unified action wire <c>AvatarActionHandler</c>/<c>AvatarActionResumeHandler</c>
///     forward for every action. See the skill-casting-cooldown-mechanics behavior contract for why casting
///     is split into these two phases.
/// </summary>
public class ZoneSkillCastTests
{
    private static SkillDefinition HolyShieldSkill(byte maxUpgradePoint, short manaUse, byte shieldPercent,
        short runTime)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(82, 0, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(82, 1, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillDefinition HealLifeSkill(byte maxUpgradePoint, short manaUse, byte healAmount)
    {
        var row = new SkillRowDto(106, "Heal", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(106, 0, manaUse, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(106, 1, manaUse, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    /// <summary>
    ///     Phase A (op15, cast-start): action-Sort 32 resolves to
    ///     <c>CharacterMotionWhitelist</c>'s skill-cast category (2) for any Type -- charges mana only, never
    ///     writes an effect.
    /// </summary>
    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 32, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = 0, SkillValue = 0
        };
    }

    /// <summary>
    ///     Phase B (op16, effect confirm): action-Sort 1, echoing the same skill number/grade Phase A last
    ///     recorded -- this is what actually writes the buff/heal effect.
    /// </summary>
    private static ActionInfo SkillEffectConfirmAction(int skillNumber, int gradeNum1, int targetCharacterId = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 1, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = targetCharacterId,
            TargetObjectUniqueNumber = unchecked((int)(uint)targetCharacterId),
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = 0, SkillValue = 0
        };
    }

    [Fact]
    public void HolyShieldCast_ConsumesManaAndWritesBuffSlot9()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana; // 300 (EnterData default), MaxLife=840

        // Phase A: charges mana, does NOT write the buff yet.
        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);

        // Phase B: echoes the same skill/grade Phase A just recorded -> writes the buff.
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana); // Phase B never (re)charges mana
        Assert.Equal(168, state.Buffs.Buff[9 * 2]); // 20% of MaxLife(840) = 168
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void EffectConfirm_WithoutMatchingCastStart_IsSilentNoOp()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        // No preceding Phase A cast-start recorded this skill/grade (the recorded action is still whatever
        // Enter defaulted it to) -- the confirm's echoed skill/grade cannot match, so this is a no-op.
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void InsufficientMana_CastFails_NoBuffWritten()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 9999, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void SecondCastWithinSameLegacyTick_IsRejectedByTheAntiFloodGate()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 10, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // first cast succeeds: -10 mana

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // still well within the same 500ms legacy tick window

        Assert.Equal(manaBefore - 10, state.Mana); // only ONE cast's worth of mana consumed
    }

    // Skill 82's own additional zone-124-only real-time reapplication cooldown (mLastHSTick, see
    // PlayerRuntimeState.LastHolyShieldAppliedUtc's and Zone.ApplySkillEffectConfirm's own remarks) --
    // distinct from, and layered on top of, the ordinary Phase A cast-start anti-flood gate already covered by
    // SecondCastWithinSameLegacyTick_IsRejectedByTheAntiFloodGate above. Every existing test in this file uses
    // zone mapId 1, where this cooldown is never active at all (MapId == 124 is part of its own trigger
    // condition) -- these two tests are this cooldown gate's only coverage.
    [Fact]
    public void HolyShieldOnZone124_SecondApplicationWithinRealTenSeconds_SkipsTheRewrite_ButStillChargesMana()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(124, worldData: worldData); // the specific zone-server process
        // this cooldown gate is scoped to (Zone.HolyShieldCooldownZoneId) -- every other mapId reapplies the
        // buff unconditionally on every cast, no minimum interval.
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 124)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        // First application: PlayerRuntimeState.LastHolyShieldAppliedUtc's own zero-init default
        // (DateTime.MinValue, not DateTime.UtcNow) means a freshly-registered avatar's very first cast on
        // this zone always clears the ten-second threshold -- applies normally, same shape as
        // HolyShieldCast_ConsumesManaAndWritesBuffSlot9 above.
        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]); // 20% of MaxLife(840) = 168
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);

        // Corrupt the buff to a sentinel so a genuine second write is unmistakable -- there is no buff-decay
        // simulation system wired into this zone (ZoneTestKit.CreateZone's default simulationSystems is
        // empty), so "unchanged" and "not rewritten" are the same observation here.
        state.Buffs.Buff[9 * 2] = -1;
        state.Buffs.Buff[9 * 2 + 1] = -1;

        // Advances only the ZONE's own simulated clock (Zone.Tick's _clock field) well past the unrelated
        // 500ms Phase A cast-start anti-flood gate (SimulationClock.LegacyTick, wired to LastSkillCastAtZoneClock)
        // -- NOT real wall-clock time, which is what the zone-124 skill-82 cooldown itself (LastHolyShieldAppliedUtc,
        // compared against a genuine DateTime.UtcNow read) is measured against. This isolates the assertion
        // below to that cooldown alone: Phase A's second cast-start must succeed (proving the two gates are
        // independent), while barely any real time has elapsed since the first application.
        zone.Tick(TimeSpan.FromMilliseconds(600));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Phase A (mana charge) is wired to a wholly separate gate/clock and succeeds again...
        Assert.Equal(manaBefore - 60, state.Mana);
        // ...but Phase B's buff WRITE is skipped: real wall-clock elapsed since the first application is
        // nowhere near HolyShieldReapplyCooldown (10s), so ApplySkillEffectConfirm's zone-124 gate breaks
        // before ApplyBuffWrites runs -- the sentinel values set above must survive untouched.
        Assert.Equal(-1, state.Buffs.Buff[9 * 2]);
        Assert.Equal(-1, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void HolyShieldOnNonZone124_SecondApplicationInQuickSuccession_StillRewritesTheBuff()
    {
        // Control case for the test above: away from mapId 124, the cooldown gate's own trigger condition
        // (MapId == HolyShieldCooldownZoneId) never matches, so a second application in quick succession DOES
        // rewrite the buff -- proving the skip above is really this gate firing, not some other unrelated
        // reason the second write might not have landed. Asserts "not the sentinel anymore" rather than a
        // specific numeric value: the first successful write's own RecomputeStatsAndBroadcastBuffs call
        // populates state.Stats for the first time, so the SECOND resolution's casterMaxLife
        // (state.Stats?.MaxLife ?? state.MaxLife) is the freshly recomputed value, not EnterData's placeholder
        // 840 default -- a real, pre-existing quirk of this bare test fixture (no equipment/proper level
        // progression seeded), unrelated to the cooldown gate under test here.
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(168, state!.Buffs.Buff[9 * 2]); // first application: matches the dedicated test above
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);

        // Sentinel, not a duration/value the resolver could ever legitimately reproduce -- makes "was this
        // slot actually written again" unambiguous regardless of what numeric value the second write lands on.
        state.Buffs.Buff[9 * 2] = -1;
        state.Buffs.Buff[9 * 2 + 1] = -1;

        zone.Tick(TimeSpan.FromMilliseconds(600)); // past the unrelated Phase A anti-flood gate only

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.NotEqual(-1, state.Buffs.Buff[9 * 2]);
        Assert.NotEqual(-1, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void TargetedHeal_RestoresTargetLifeClampedToMax()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Life = 700; // MaxLife=840 -> 140 of headroom, less than the 100 flat heal

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(800, target.Life);
    }

    [Fact]
    public void TargetedHeal_ClampsToMaxLife()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 200)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Life = 800; // MaxLife=840 -> only 40 of headroom, less than the 200 flat heal

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(840, target.Life);
    }
}
