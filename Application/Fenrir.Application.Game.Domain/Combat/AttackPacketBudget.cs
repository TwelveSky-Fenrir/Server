using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Combat;

public static class AttackPacketBudget
{
    public static bool TryConsume(PlayerRuntimeState state, int attackActionValue4, bool countAttempt = true)
    {
        if (!state.AttackBudgetEnforced)
            return true;

        if (countAttempt)
        {
            state.AttackSubPacketsUsed++;
            if (state.AttackSubPacketsUsed > state.AttackSubPacketCeiling)
                return false;
        }

        return attackActionValue4 == state.ActionSort;
    }
}
