using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
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
/// <remarks>
///     D2 hook 4 (<c>Zone.EvaluateSkillCastTamperGuard</c>) now runs ahead of every Phase-A cast-start in this
///     file, so every caster exercising a real cast below must have a matching skill-bound hotkey seeded first
///     (<see cref="SeedSkillHotkey" />) -- a bare <c>ZoneTestKit.EnterData</c> caster has no hotkeys at all and
///     would otherwise be rejected as a <c>HotkeyMismatch</c> tamper offense before ever reaching mana-charge
///     logic. This mirrors the real client contract the guard enforces: a skill cast must be backed by a
///     hotkey bound to the exact (skill#, invested grade) pair claimed on the wire.
/// </remarks>
public class ZoneSkillCastTests
{
    /// <summary>
    ///     Seeds a skill-bound hotkey matching <paramref name="skillId" />/<paramref name="investedGrade" /> so
    ///     <c>Zone.EvaluateSkillCastTamperGuard</c>'s <c>HotkeyMismatch</c> check passes for a subsequent
    ///     <see cref="SkillCastStartAction" /> using the same pair -- see this class's own remarks. Also seeds a
    ///     matching <see cref="LearnedSkill" /> slot at the same invested grade: since the D2 special-case
    ///     exclusion set was wired in (wave15), <c>SkillGradeAuthority.GetMaxSkillGradeNum</c> returns -1 for an
    ///     unlearned skill, and any positive claimed invested grade would then trip <c>SkillHack1</c> against a
    ///     -1 server max -- a real client can only ever have a skill hotkeyed if it has actually learned that
    ///     skill at that grade, so this mirrors that precondition rather than leaving it unrealistically absent.
    /// </summary>
    private static void SeedSkillHotkey(PlayerRuntimeState state, int skillId, int investedGrade, byte page = 0,
        byte index = 0)
    {
        state.Hotkeys = state.Hotkeys.SetItem((page, index), new HotkeySlot(HotkeyBindingKind.Skill, skillId,
            investedGrade));
        state.LearnedSkills = state.LearnedSkills.SetItem(index, new LearnedSkill(skillId, investedGrade));
    }

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

    /// <summary>
    ///     Holy Shield with distinct per-grade ManaUse/ShieldLifeUp so a mismatch between the grade used for
    ///     the mana cost (invested only) and the grade used for the effect (invested + item bonus) is
    ///     observable -- unlike <see cref="HolyShieldSkill" />, which holds both constant across grades.
    /// </summary>
    private static SkillDefinition HolyShieldSkillGraded(byte maxUpgradePoint,
        (short Mana, byte Shield, short RunTime) grade0, (short Mana, byte Shield, short RunTime) grade1)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var g0 = new SkillGradeRowDto(82, 0, grade0.Mana, 0, 0, 0, 0, 0, 0, 0, 0, grade0.RunTime, 0, 0, 0, 0, 0, 0,
            0, 0, 0, grade0.Shield, 0, 0, 0, 0, 0);
        var g1 = new SkillGradeRowDto(82, 1, grade1.Mana, 0, 0, 0, 0, 0, 0, 0, 0, grade1.RunTime, 0, 0, 0, 0, 0, 0,
            0, 0, 0, grade1.Shield, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [g0, g1]);
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
    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 32, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
        };
    }

    /// <summary>
    ///     Phase B (op16, effect confirm): action-Sort 1, echoing the same skill number/grade Phase A last
    ///     recorded -- this is what actually writes the buff/heal effect.
    /// </summary>
    private static ActionInfo SkillEffectConfirmAction(int skillNumber, int gradeNum1, int targetCharacterId = 0,
        int gradeNum2 = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 1, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = targetCharacterId,
            TargetObjectUniqueNumber = unchecked((int)(uint)targetCharacterId),
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
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
        SeedSkillHotkey(state, 82, 10);

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

    /// <summary>
    ///     Legacy-parity regression: a skill cast's MANA cost is derived from the caster's invested grade
    ///     points (<c>aSkillGradeNum1</c>) ALONE, while the buff/heal EFFECT is derived from the combined
    ///     grade (<c>aSkillGradeNum1 + aSkillGradeNum2</c>, the item-granted <c>GetBonusSkillValue</c> the
    ///     client echoes). Réf. C++ : <c>Server/ts25zone/S04_MyWork02.cpp:1640</c> (op15 mana charge passes
    ///     <c>aSkillGradeNum1</c> alone to <c>ReturnSkillValue</c> factor 1) vs
    ///     <c>Server/ts25zone/S07_MyGame03.cpp:9398-9399</c> (<c>ProcessForCreateBuff</c> case 82 computes the
    ///     shield value/duration from <c>aSkillGradeNum1 + aSkillGradeNum2</c>). Before the fix, Phase A
    ///     charged the combined grade -- over-charging every player holding a skill-bonus item.
    /// </summary>
    [Fact]
    public void SkillCast_ManaUsesInvestedGradeOnly_EffectUsesInvestedPlusItemBonusGrade()
    {
        // ManaUse interpolates 0 (grade0) -> 100 (grade1) over MaxUpgradePoint 10; ShieldLifeUp 0 -> 20%.
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkillGraded(10, (0, 0, 40), (100, 20, 40))
        }.ToFrozenDictionary();
        // D2 hook 4: the caster's CLAIMED item-bonus grade (num2=5 below) must be backed by a real equipped
        // item granting +5 to skill 82, or Zone.EvaluateSkillCastTamperGuard's server-computed
        // SkillGradeAuthority.GetBonusSkillValue (0, no equipment) would disagree with the claim and reject
        // this as a BonusGradeMismatch offense before Phase A ever runs.
        var bonusItem = new ItemDefinition(WorldDataTestRows.Item(9000),
            [new ItemBonusSkillRowDto(9000, 0, 82, 5)]);
        var itemsById = new Dictionary<int, ItemDefinition> { [9000] = bonusItem }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById, itemsById: itemsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana; // 300 (EnterData default), MaxLife=840
        SeedSkillHotkey(state, 82, 5); // invested grade (num1) is the hotkey's own stored grade, not the combined one
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty.Add(0, new ItemStack(9000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        // Invested points num1=5, item-bonus num2=5 -> combined grade 10 (== MaxUpgradePoint, the max value).
        // Phase A charges from num1=5 alone: ReturnSkillValue(sPoint=5) = 0 + (100-0)*5/10 = 50.
        // The pre-fix bug charged the combined grade 10 -> ReturnSkillValue(10) = 100.
        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 5, 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 50, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]); // effect not written until Phase B

        // Phase B echoes num1=5, num2=5 -> effect uses the combined grade 10: ShieldLifeUp = 20% of MaxLife
        // (840) = 168, proving the effect still honors the full invested+bonus grade after the mana fix.
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 5, 0, 5), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 50, state.Mana); // Phase B never (re)charges mana
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
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
        SeedSkillHotkey(state, 82, 10);

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
        SeedSkillHotkey(state, 82, 10);

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
        SeedSkillHotkey(state, 82, 10);

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
        SeedSkillHotkey(state!, 82, 10);

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
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

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
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(840, target.Life);
    }
}
