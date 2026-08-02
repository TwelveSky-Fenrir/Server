using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Combat;

public static class AttackPacketBudget
{
    public static bool TryConsume(PlayerRuntimeState state, int attackActionValue4, bool enforceCeiling = true)
    {
        if (!enforceCeiling || !state.AttackBudgetEnforced)
            return true;

        if (state.AttackSubPacketsUsed > state.AttackSubPacketCeiling)
            return false;

        state.AttackSubPacketsUsed++;

        return attackActionValue4 == state.ActionSort;
    }
}
