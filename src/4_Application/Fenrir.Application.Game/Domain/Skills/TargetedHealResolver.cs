using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Skills;

public static class TargetedHealResolver
{
    public static bool TryResolveAmount(int casterCharacterId, uint expectedUniqueNumber, in Target target,
        int rawAmount, out int appliedAmount)
    {
        appliedAmount = 0;

        if (casterCharacterId == target.CharacterId)
            return false;
        if (target.UniqueNumber != expectedUniqueNumber)
            return false;
        if (target.IsDead || target.IsStunned || target.IsHidden || target.ShopOpen)
            return false;
        if (!CombatResolver.IsTargetableActionState(target.ActionSort))
            return false;
        if (target.CurrentValue >= target.MaxValue)
            return false;

        var amount = Math.Min(rawAmount, target.MaxValue - target.CurrentValue);
        if (amount < 1)
            return false;

        appliedAmount = amount;
        return true;
    }

    public readonly record struct Target(
        int CharacterId,
        uint UniqueNumber,
        bool IsDead,
        bool IsStunned,
        bool IsHidden,
        bool ShopOpen,
        int ActionSort,
        int CurrentValue,
        int MaxValue);
}
