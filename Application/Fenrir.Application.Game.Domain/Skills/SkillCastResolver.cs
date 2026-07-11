using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <remarks>
///     <see cref="TryCast" />'s mana-charge step is deliberately independent of whether
///     <see cref="SkillEffectCatalog" /> has a registered effect for the cast skill id. Legacy's own op15
///     mana debit (Server/ts25zone/S04_MyWork02.cpp:1640-1650,1680-1683) is computed purely from the
///     caster's invested grade points against the skill's own "ManaUse" grade-table factor, and is charged
///     unconditionally -- it never inspects whether a later, separate confirm-step dispatch
///     (Server/ts25zone/S07_MyGame04.cpp:1509-1564) recognizes the skill id at all. A prior revision of this
///     resolver collapsed a catalog miss straight to <see cref="FailureReason.NotCastable" /> before ever
///     computing the mana cost, which silently made every uncatalogued skill id (starter skills 1/20/39,
///     the six FastRunSpeed ids 2/3/21/22/40/41, and the six stun ids 4/5/23/24/42/43) free to spam-cast for
///     zero mana in Fenrir -- a real, if narrow, economy divergence from legacy for the six FastRunSpeed and
///     six stun ids specifically (their ManaUse grade rows are genuinely non-zero;
///     Database/Migrations/Seed/world/072_skill_grades.sql:15-18,53-56,91-94 and :19-22,57-58,95-96 -- the
///     starter ids 1/20/39 are unaffected either way since their own ManaUse rows are 0 at both grades).
///     <see cref="SkillEffectKind.None" /> is the outcome for this case: mana is still charged (subject to
///     the ordinary insufficient-mana check below), but no buff/heal effect is produced -- the caller's op16
///     confirm-step switch (no case, no default, for <see cref="SkillEffectKind.None" />) is a no-op for it,
///     matching legacy's own silent fallthrough exactly.
/// </remarks>
public static class SkillCastResolver
{
    public enum FailureReason
    {
        None,
        UnknownSkill,

        /// <summary>
        ///     No longer produced by <see cref="TryCast" /> -- a catalog miss now succeeds with
        ///     <see cref="SkillEffectKind.None" /> instead (see this class's own remarks). Retained on the
        ///     enum for source/binary compatibility with any external pattern match against this value.
        /// </summary>
        NotCastable,
        InsufficientMana,
        WrongWeaponClass
    }

        public static Result TryCast(SkillDefinition? skill, int gradePoints, int casterMana, int casterMaxLife,
        int? equippedWeaponSort, int supportSkillTimeUpRatio)
    {
        if (skill is not { } skillDef)
            return Result.Fail(FailureReason.UnknownSkill);

        var manaCost = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, SkillValueKind.ManaUse);
        if (casterMana < manaCost)
            return Result.Fail(FailureReason.InsufficientMana);

        if (!SkillEffectCatalog.TryGet(skillDef.Skill.SkillId, out var effect))
            return new Result(true, FailureReason.None, manaCost, ImmutableArray<BuffWrite>.Empty,
                SkillEffectKind.None, 0);

        if (effect.Kind != SkillEffectKind.SelfBuff)
        {
            var healKind = effect.Kind == SkillEffectKind.HealLife
                ? SkillValueKind.RecoverInfo1
                : SkillValueKind.RecoverInfo2;
            var healAmount = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, healKind);
            return new Result(true, FailureReason.None, manaCost, ImmutableArray<BuffWrite>.Empty, effect.Kind,
                healAmount, effect.RequiresFullParty);
        }

        if (!effect.RequiredWeaponSorts.IsEmpty &&
            (equippedWeaponSort is not { } sort || !effect.RequiredWeaponSorts.Contains(sort)))
            return Result.Fail(FailureReason.WrongWeaponClass);

        var baseDurationTicks = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, SkillValueKind.RunTime);
        var durationTicks = effect.AppliesSupportSkillTimeUpRatio
            ? baseDurationTicks * supportSkillTimeUpRatio
            : baseDurationTicks;
        var writes = ImmutableArray.CreateBuilder<BuffWrite>(effect.BuffSlots.Length);
        foreach (var slot in effect.BuffSlots)
        {
            var raw = SkillCatalog.ReturnSkillValue(skillDef, gradePoints, slot.Kind);
            var value = slot.IsPercentOfMaxLife ? (int)(raw * casterMaxLife * 0.01f) : (int)raw;
            writes.Add(new BuffWrite(slot.Slot, value, durationTicks));
        }

        return new Result(true, FailureReason.None, manaCost, writes.ToImmutable(), effect.Kind, 0,
            effect.RequiresFullParty);
    }

    public readonly record struct BuffWrite(int Slot, int Value, int DurationTicks);

    public readonly record struct Result(
        bool Success,
        FailureReason Failure,
        int ManaCost,
        ImmutableArray<BuffWrite> BuffWrites,
        SkillEffectKind Kind,

                int HealAmount,

                bool RequiresFullParty = false)
    {
        public static Result Fail(FailureReason reason)
        {
            return new Result(false, reason, 0, ImmutableArray<BuffWrite>.Empty, SkillEffectKind.None, 0);
        }
    }
}
