using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class HotkeyItemConsumptionResolver
{
    public enum EffectKind
    {
        None,

        Life,
        Mana,
        LifeAndMana,

        Buff
    }

    public enum Outcome
    {
        Disconnect,

        RejectedClean,

        Success
    }

    private const int DarkAttackBuffSlot = 15;

    private const int HitRateBuffSlot = 17;

    private const int DodgeRateBuffSlot = 18;

    private const int DarkAttackBuffPercent = 3;

    private const int HitOrDodgeBuffPercent = 25;

    public const int MaxPotionSortNum = 16;

    public const byte ConsumableItemCategory = 2;

    private static readonly int AssassinScrollDurationTicks =
        SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(40));

    private static readonly int SixtySecondBuffDurationTicks =
        SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(60));

    public static Result Resolve(
        int page, int index, HotkeySlot slot,
        bool isStunned, bool isDead, bool canUseConsumables,
        bool itemResolved, byte itemCategory, int potionType1, int potionType2,
        int life, int effectiveMaxLife, int mana, int effectiveMaxMana)
    {
        if (!HotkeyActionResolver.IsValidPage(page) || !HotkeyActionResolver.IsValidIndex(index))
            return Result.DisconnectResult;

        if (slot.Kind != HotkeyBindingKind.Item)
            return Result.DisconnectResult;

        if (isStunned || isDead)
            return Result.RejectedCleanResult;

        if (!itemResolved || itemCategory != ConsumableItemCategory)
            return Result.DisconnectResult;

        if (slot.Value2 < 1)
            return Result.DisconnectResult;

        switch (potionType1)
        {
            case 1:
            case 2:
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (life >= effectiveMaxLife)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(potionType1 == 2, potionType2, effectiveMaxLife, life);
                return Succeed(slot, EffectKind.Life, gain, 0);
            }
            case 3:
            case 4:
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (mana >= effectiveMaxMana)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(potionType1 == 4, potionType2, effectiveMaxMana, mana);
                return Succeed(slot, EffectKind.Mana, 0, gain);
            }
            case 5:
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (life >= effectiveMaxLife && mana >= effectiveMaxMana)
                    return Result.RejectedCleanResult;
                var lifeGain = ComputeClampedGain(true, potionType2, effectiveMaxLife, life);
                var manaGain = ComputeClampedGain(true, potionType2, effectiveMaxMana, mana);
                return Succeed(slot, EffectKind.LifeAndMana, lifeGain, manaGain);
            }
            case 9:
                return Succeed(slot, EffectKind.None, 0, 0);

            case 12:
                return SucceedWithBuff(slot, DarkAttackBuffSlot, DarkAttackBuffPercent, AssassinScrollDurationTicks);
            case 13:
                return SucceedWithBuff(slot, DarkAttackBuffSlot, DarkAttackBuffPercent, SixtySecondBuffDurationTicks);
            case 14:
                return SucceedWithBuff(slot, HitRateBuffSlot, HitOrDodgeBuffPercent, SixtySecondBuffDurationTicks);
            case 15:
                return SucceedWithBuff(slot, DodgeRateBuffSlot, HitOrDodgeBuffPercent, SixtySecondBuffDurationTicks);

            case 6:
            case 16:
                return Result.RejectedCleanResult;

            default:
                return Result.DisconnectResult;
        }
    }

    private static Result Succeed(HotkeySlot slot, EffectKind effect, int lifeGain, int manaGain)
    {
        var remaining = slot.Value2 - 1;
        var newSlot = remaining > 0 ? slot with { Value2 = remaining } : HotkeySlot.Empty;
        return new Result(Outcome.Success, newSlot, effect, lifeGain, manaGain,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty);
    }

    private static Result SucceedWithBuff(HotkeySlot slot, int buffSlot, int value, int durationTicks)
    {
        var remaining = slot.Value2 - 1;
        var newSlot = remaining > 0 ? slot with { Value2 = remaining } : HotkeySlot.Empty;
        var write = ImmutableArray.Create(new SkillCastResolver.BuffWrite(buffSlot, value, durationTicks));
        return new Result(Outcome.Success, newSlot, EffectKind.Buff, 0, 0, write);
    }

    private static int ComputeClampedGain(bool isPercent, int potionType2, int effectiveMax, int current)
    {
        var raw = isPercent ? effectiveMax * potionType2 / 100 : potionType2;
        var headroom = effectiveMax - current;
        return Math.Clamp(raw, 0, headroom);
    }

    public readonly record struct Result(
        Outcome Outcome,
        HotkeySlot NewSlot,
        EffectKind Effect,
        int LifeGain,
        int ManaGain,
        ImmutableArray<SkillCastResolver.BuffWrite> BuffWrites)
    {
        public static readonly Result DisconnectResult = new(Outcome.Disconnect, default, EffectKind.None, 0, 0,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty);

        public static readonly Result RejectedCleanResult = new(Outcome.RejectedClean, default, EffectKind.None, 0,
            0, ImmutableArray<SkillCastResolver.BuffWrite>.Empty);
    }
}
