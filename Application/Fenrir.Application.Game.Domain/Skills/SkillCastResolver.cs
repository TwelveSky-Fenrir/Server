using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Pure resolution of one non-attack skill cast (op15 CZ_AVATAR_ACTION_SEND, action-category Sort
///     resolving to the real skill-cast category -- Sorts 32, 33, 38-90, NOT action-Sort 30, which is the
///     unrelated stand-up-from-death request): MP-cost check, weapon-class gate, and buff-value/heal-amount
///     computation. Never touches PlayerRuntimeState directly -- Zone.ApplySkillCastManaCharge (mana only)
///     and Zone.ApplySkillEffectConfirm (effect write, on a matching later op16 confirmation) apply the
///     result; see the skill-casting-cooldown-mechanics behavior contract for why this is split in two.
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

    /// <summary>
    ///     <paramref name="equippedWeaponSort" /> is the caster's current EWEAPON slot's Sort, or null if
    ///     unequipped. <paramref name="supportSkillTimeUpRatio" /> is the caller's already-computed, cached
    ///     <c>mSupportSkillTimeUpRatio</c> (<see cref="SupportSkillTimeUpRatioCalculator" /> /
    ///     <see cref="World.PlayerRuntimeState.SupportSkillTimeUpRatio" />) -- this method only ever reads it,
    ///     never recomputes it (behavior contract "buff-application-stacking-decay": application of the
    ///     multiplier and recomputation of the multiplier are separate concerns).
    /// </summary>
    public static Result TryCast(SkillDefinition? skill, int gradePoints, int casterMana, int casterMaxLife,
        int? equippedWeaponSort, int supportSkillTimeUpRatio)
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
                healAmount, effect.RequiresFullParty);
        }

        if (!effect.RequiredWeaponSorts.IsEmpty &&
            (equippedWeaponSort is not { } sort || !effect.RequiredWeaponSorts.Contains(sort)))
            return Result.Fail(FailureReason.WrongWeaponClass);

        var baseDurationTicks = (int)SkillCatalog.ReturnSkillValue(skillDef, gradePoints, SkillValueKind.RunTime);
        // mSupportSkillTimeUpRatio (S03_MyUser.cpp:524-541) multiplies duration only, never the paired value
        // computed in the loop below -- and only for the 14 genuine self-buff sites, not the 4 formation
        // skills collapsed to self (SkillEffectCatalog's own AppliesSupportSkillTimeUpRatio remarks).
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
        /// <summary>Meaningful only for HealLife/HealMana -- the raw, unclamped amount; the caller clamps to the target's remaining capacity.</summary>
        int HealAmount,
        /// <summary>
        ///     Mirrors <see cref="SkillEffectDefinition.RequiresFullParty" /> for the resolved skill -- true only
        ///     for the 4 Formation party-buff skills (76/77/79/81). The caller must additionally confirm an
        ///     exactly-full 5-member party is present before applying <see cref="BuffWrites" />; this resolver
        ///     has no party/zone visibility of its own to do that check itself.
        /// </summary>
        bool RequiresFullParty = false)
    {
        public static Result Fail(FailureReason reason)
        {
            return new Result(false, reason, 0, ImmutableArray<BuffWrite>.Empty, SkillEffectKind.SelfBuff, 0);
        }
    }
}
