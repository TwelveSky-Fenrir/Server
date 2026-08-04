using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class HotkeyItemConsumptionResolver
{
    public enum AntiCheatMarkerKind
    {
        None,

        DarkAttack,

        HitRate,

        DodgeRate
    }

    public enum EffectKind
    {
        None,

        Life,
        Mana,
        LifeAndMana,

        Buff,

        PetActivity,
        MountActivity
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

    public const int AssassinScrollMarkerValue = 1;

    public const int DepartedSpiritScrollMarkerValue = 2;

    public const int HitOrDodgeBuffMarkerValue = 1;

    public const int MaxPotionSortNum = 16;

    public const byte ConsumableItemCategory = 2;

    private const int AssassinScrollBuffDurationGatePeriods = 40;

    private const int StandardScrollOrBookBuffDurationGatePeriods = 60;

    public static Result Resolve(
        int page, int index, HotkeySlot slot,
        bool isStunned, bool isDead, bool canUseConsumables,
        bool itemResolved, byte itemCategory, int potionType1, int potionType2,
        int life, int effectiveMaxLife, int mana, int effectiveMaxMana,
        bool isDarkAttackScrollZoneAllowed, int currentDarkAttackMarker,
        bool petFoodEligible, int petActivity, bool mountFoodEligible, int mountActivity)
    {
        if (!HotkeyActionResolver.IsValidPage(page) || !HotkeyActionResolver.IsValidIndex(index))
            return Result.DisconnectResult;

        if (slot.Kind != HotkeyBindingKind.Item)
            return Result.RejectedCleanResult;

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
            {
                if (!isDarkAttackScrollZoneAllowed || currentDarkAttackMarker == DepartedSpiritScrollMarkerValue)
                    return Result.RejectedCleanResult;
                return SucceedWithBuff(slot, DarkAttackBuffSlot, DarkAttackBuffPercent,
                    AssassinScrollBuffDurationGatePeriods,
                    AntiCheatMarkerKind.DarkAttack, AssassinScrollMarkerValue, false);
            }
            case 13:
            {
                if (!isDarkAttackScrollZoneAllowed || currentDarkAttackMarker == AssassinScrollMarkerValue)
                    return Result.RejectedCleanResult;
                return SucceedWithBuff(slot, DarkAttackBuffSlot, DarkAttackBuffPercent,
                    StandardScrollOrBookBuffDurationGatePeriods,
                    AntiCheatMarkerKind.DarkAttack, DepartedSpiritScrollMarkerValue, false);
            }
            case 14:
                return SucceedWithBuff(slot, HitRateBuffSlot, HitOrDodgeBuffPercent,
                    StandardScrollOrBookBuffDurationGatePeriods,
                    AntiCheatMarkerKind.HitRate, HitOrDodgeBuffMarkerValue, true);
            case 15:
                return SucceedWithBuff(slot, DodgeRateBuffSlot, HitOrDodgeBuffPercent,
                    StandardScrollOrBookBuffDurationGatePeriods,
                    AntiCheatMarkerKind.DodgeRate, HitOrDodgeBuffMarkerValue, true);

            case 6:
            {
                if (!petFoodEligible || petActivity >= MountActivityExpCodec.MaxActivity)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(false, potionType2, MountActivityExpCodec.MaxActivity, petActivity);
                if (gain <= 0)
                    return Result.RejectedCleanResult;
                return SucceedWithPetActivityGain(slot, gain);
            }

            case 16:
            {
                if (!mountFoodEligible)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(false, potionType2, MountActivityExpCodec.MaxActivity, mountActivity);
                if (gain <= 0)
                    return Result.RejectedCleanResult;
                return SucceedWithMountActivityGain(slot, gain);
            }

            default:
                return Result.DisconnectResult;
        }
    }

    private static Result Succeed(HotkeySlot slot, EffectKind effect, int lifeGain, int manaGain)
    {
        return new Result(Outcome.Success, Decrement(slot), effect, lifeGain, manaGain,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty, AntiCheatMarkerKind.None, 0, false);
    }

    private static Result SucceedWithBuff(HotkeySlot slot, int buffSlot, int value, int durationTicks,
        AntiCheatMarkerKind markerKind, int markerValue, bool recomputeStats)
    {
        var write = ImmutableArray.Create(new SkillCastResolver.BuffWrite(buffSlot, value, durationTicks));
        return new Result(Outcome.Success, Decrement(slot), EffectKind.Buff, 0, 0, write, markerKind, markerValue,
            recomputeStats);
    }

    private static Result SucceedWithPetActivityGain(HotkeySlot slot, int gain)
    {
        return new Result(Outcome.Success, Decrement(slot), EffectKind.PetActivity, 0, 0,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty, AntiCheatMarkerKind.None, 0, false,
            gain);
    }

    private static Result SucceedWithMountActivityGain(HotkeySlot slot, int gain)
    {
        return new Result(Outcome.Success, Decrement(slot), EffectKind.MountActivity, 0, 0,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty, AntiCheatMarkerKind.None, 0, false,
            MountActivityGain: gain);
    }

    private static HotkeySlot Decrement(HotkeySlot slot)
    {
        var remaining = slot.Value2 - 1;
        return remaining > 0 ? slot with { Value2 = remaining } : HotkeySlot.Empty;
    }

    private static int ComputeClampedGain(bool isPercent, int potionType2, int effectiveMax, int current)
    {
        var raw = isPercent ? effectiveMax * potionType2 / 100 : potionType2;
        var headroom = Math.Max(0, effectiveMax - current);
        return Math.Clamp(raw, 0, headroom);
    }

    public readonly record struct Result(
        Outcome Outcome,
        HotkeySlot NewSlot,
        EffectKind Effect,
        int LifeGain,
        int ManaGain,
        ImmutableArray<SkillCastResolver.BuffWrite> BuffWrites,
        AntiCheatMarkerKind MarkerKind,
        int MarkerValue,
        bool RecomputeStats,
        int PetActivityGain = 0,
        int MountActivityGain = 0)
    {
        public static readonly Result DisconnectResult = new(Outcome.Disconnect, default, EffectKind.None, 0, 0,
            ImmutableArray<SkillCastResolver.BuffWrite>.Empty, AntiCheatMarkerKind.None, 0, false);

        public static readonly Result RejectedCleanResult = new(Outcome.RejectedClean, default, EffectKind.None, 0,
            0, ImmutableArray<SkillCastResolver.BuffWrite>.Empty, AntiCheatMarkerKind.None, 0, false);
    }
}
