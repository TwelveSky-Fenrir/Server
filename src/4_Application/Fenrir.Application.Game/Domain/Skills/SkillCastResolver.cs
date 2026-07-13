using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

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

    public static Result TryCast(SkillDefinition? skill, int gradePoints, int casterMana, int casterMaxLife,
        int? equippedWeaponSort, int supportSkillTimeUpRatio, int manaReductionRatioPercent = 0)
    {
        if (skill is not { } skillDef)
            return Result.Fail(FailureReason.UnknownSkill);

        var manaCost = ComputeManaCost(skillDef, gradePoints, manaReductionRatioPercent);
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

    private static int ComputeManaCost(SkillDefinition skill, int gradePoints, int manaReductionRatioPercent)
    {
        var rawCost = (int)SkillCatalog.ReturnSkillValue(skill, gradePoints, SkillValueKind.ManaUse);
        return manaReductionRatioPercent > 0
            ? rawCost - rawCost * manaReductionRatioPercent / 100
            : rawCost;
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
