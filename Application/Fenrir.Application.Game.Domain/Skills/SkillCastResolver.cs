using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Pure resolution of one non-attack skill cast (AVATAR_ACTION_SEND Sort=30): MP-cost check, weapon-class
///     gate, and buff-value/heal-amount computation. Never touches PlayerRuntimeState directly -- Zone.ApplySkillCast
///     applies the result, including clamping a targeted heal against the live target's current HP/MP.
/// </summary>
public static class SkillCastResolver
{
    public enum FailureReason
    {
        None,
        UnknownSkill,
        NotCastable,
        InsufficientMana,
        WrongWeaponClass
    }

    /// <summary><paramref name="equippedWeaponSort" /> is the caster's current EWEAPON slot's Sort, or null if unequipped.</summary>
    public static Result TryCast(SkillDefinition? skill, int gradePoints, int casterMana, int casterMaxLife,
        int? equippedWeaponSort)
    {
        if (skill is not { } skillDef)
            return Result.Fail(FailureReason.UnknownSkill);

        if (!SkillEffectCatalog.TryGet(skillDef.Skill.SkillId, out var effect))
            return Result.Fail(FailureReason.NotCastable);

        var manaCost = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, SkillValueKind.ManaUse);
        if (casterMana < manaCost)
            return Result.Fail(FailureReason.InsufficientMana);

        if (effect.Kind != SkillEffectKind.SelfBuff)
        {
            var healKind = effect.Kind == SkillEffectKind.HealLife
                ? SkillValueKind.RecoverInfo1
                : SkillValueKind.RecoverInfo2;
            var healAmount = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, healKind);
            return new Result(true, FailureReason.None, manaCost, ImmutableArray<BuffWrite>.Empty, effect.Kind,
                healAmount);
        }

        if (!effect.RequiredWeaponSorts.IsEmpty &&
            (equippedWeaponSort is not { } sort || !effect.RequiredWeaponSorts.Contains(sort)))
            return Result.Fail(FailureReason.WrongWeaponClass);

        var durationTicks = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, SkillValueKind.RunTime);
        var writes = ImmutableArray.CreateBuilder<BuffWrite>(effect.BuffSlots.Length);
        foreach (var slot in effect.BuffSlots)
        {
            var raw = SkillCatalog.ReturnSkillValue(skillDef, gradePoints, slot.Kind);
            var value = slot.IsPercentOfMaxLife ? (int)(raw * casterMaxLife * 0.01f) : (int)raw;
            writes.Add(new BuffWrite(slot.Slot, value, durationTicks));
        }

        return new Result(true, FailureReason.None, manaCost, writes.ToImmutable(), effect.Kind, 0);
    }

    public readonly record struct BuffWrite(int Slot, int Value, int DurationTicks);

    public readonly record struct Result(
        bool Success,
        FailureReason Failure,
        int ManaCost,
        ImmutableArray<BuffWrite> BuffWrites,
        SkillEffectKind Kind,
        /// <summary>Meaningful only for HealLife/HealMana -- the raw, unclamped amount; the caller clamps to the target's remaining capacity.</summary>
        int HealAmount)
    {
        public static Result Fail(FailureReason reason)
        {
            return new Result(false, reason, 0, ImmutableArray<BuffWrite>.Empty, SkillEffectKind.SelfBuff, 0);
        }
    }
}
