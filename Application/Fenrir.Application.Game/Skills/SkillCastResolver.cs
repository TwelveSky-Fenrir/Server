using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Pure resolution of one non-attack skill cast (<c>AVATAR_ACTION_SEND</c> Sort=30, report 12 §4.2) --
///     MP-cost check, weapon-class gate, and buff-value/heal-amount computation via
///     <see cref="SkillCatalog" />/<see cref="SkillEffectCatalog" />. Never touches
///     <c>PlayerRuntimeState</c> directly (mirrors <see cref="Fenrir.Application.Game.Combat.CombatResolver" />'s
///     own pure input-in/outcome-out shape) -- <c>Zone.ApplySkillCast</c> applies the result, including
///     resolving/clamping a targeted heal against the LIVE target's current HP/MP, which this resolver has no
///     access to by design.
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

    public readonly record struct BuffWrite(int Slot, int Value, int DurationTicks);

    public readonly record struct Result(
        bool Success,
        FailureReason Failure,
        int ManaCost,
        ImmutableArray<BuffWrite> BuffWrites,
        SkillEffectKind Kind,
        /// <summary>Meaningful only for <see cref="SkillEffectKind.HealLife" />/<see cref="SkillEffectKind.HealMana" /> -- the RAW (unclamped) flat amount; the caller clamps to the target's remaining capacity, exactly like the legacy does at its own call site.</summary>
        int HealAmount)
    {
        public static Result Fail(FailureReason reason)
        {
            return new Result(false, reason, 0, ImmutableArray<BuffWrite>.Empty, SkillEffectKind.SelfBuff, 0);
        }
    }

    /// <summary>
    ///     <paramref name="equippedWeaponSort" /> is the caster's current EWEAPON slot's <c>world.Items.Sort</c>,
    ///     or null if no weapon is equipped -- resolved by the caller (Zone already has
    ///     <see cref="WorldDataCache" /> + the caster's live Equipment container).
    /// </summary>
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
            // Targeted heal (skills 106-111, report S07_MyGame03.cpp:9449-9576): flat amount from RecoverInfo1/2.
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
}
