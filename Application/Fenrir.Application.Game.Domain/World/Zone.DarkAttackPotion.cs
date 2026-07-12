using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int DarkAttackPotionAttackerBuffSlot = 15;

    private const int DarkAttackPotionDefenderEffectSlot = 16;

    private readonly List<int> _darkAttackDebuffNeighborScratch = [];

    private void TryApplyDarkAttackPotionProc(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        var attackerBuffActive = attackerState.Buffs.Buff[DarkAttackPotionAttackerBuffSlot * 2 + 1] > 0;
        var attackerBuffProbability = attackerState.Buffs.Buff[DarkAttackPotionAttackerBuffSlot * 2];

        if (!DarkAttackPotionResolver.ShouldApply(attackerBuffActive, attackerBuffProbability,
                defenderState.IsUnderDarkAttackPotionDebuff, _random))
            return;

        defenderState.IsUnderDarkAttackPotionDebuff = true;
        defenderState.DarkAttackDebuffAccumulatorTicks = 0;
        defenderState.CanUseConsumables = false;

        BroadcastDarkAttackDebuffState(defenderState);
    }

    public void ClearDarkAttackPotionDebuff(PlayerRuntimeState state)
    {
        if (!state.IsUnderDarkAttackPotionDebuff)
            return;

        state.IsUnderDarkAttackPotionDebuff = false;
        state.CanUseConsumables = true;
        state.DarkAttackDebuffAccumulatorTicks = 0;

        BroadcastDarkAttackDebuffState(state);
    }

    private void BroadcastDarkAttackDebuffState(PlayerRuntimeState state)
    {
        SendAvatarAction(state.Session, state);

        _darkAttackDebuffNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_darkAttackDebuffNeighborScratch, state.CurrentCell, state.CharacterId,
            state.PosX, state.PosY, state.PosZ);
        BroadcastAvatarAction(_darkAttackDebuffNeighborScratch, state);
    }
}
