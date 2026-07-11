using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const short ReflectDisabledZoneId = 124;

    private bool TryApplyReflectAndDestroyer(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState,
        in AttackOutcome outcome, CrossAvatarAttackKind kind)
    {
        var preElementMainDamage = outcome.ViewDamage - outcome.ElementDamage;

        var reflect = ReflectResolver.Resolve(
            attackerState.Buffs.Buff[ReflectResolver.DestroyerBuffSlot * 2],
            defenderState.Buffs.Buff[ReflectResolver.ReflectBuffSlot * 2],
            attackerState.Level,
            defenderState.Level,
            0,
            MapId != ReflectDisabledZoneId,
            preElementMainDamage,
            _random);

        if (reflect.DestroyerSucceeded)
            RemoveDefenderHolyShields(defenderState);

        if (!reflect.ReflectFired)
            return false;

        attackerState.Life -= reflect.ReflectDamage;
        attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (attackerState.Life <= 0)
            switch (kind)
            {
                case CrossAvatarAttackKind.EnemyTribe:
                    ApplyPvpKillRewards(defenderState, attackerState);
                    RecordEnemyKillForFeed(defenderState, attackerState, false,
                        regularWarActiveMapTracker?.IsBattleInProgress(MapId) == true ||
                        MapId == KillFeedZoneCatalog.FfaMapNumber);
                    ApplyDeath(attackerState.CharacterId, DeathCause.PlayerKill);
                    break;
                case CrossAvatarAttackKind.Duel:
                    ApplyDeath(attackerState.CharacterId, DeathCause.Duel);
                    break;
            }

        return true;
    }

    private (int View, int Real) ApplyHolyShieldAbsorption(PlayerRuntimeState defenderState, in AttackOutcome outcome)
    {
        var preElementMainDamage = outcome.ViewDamage - outcome.ElementDamage;
        var absorbed = HolyShieldResolver.Absorb(defenderState.Buffs.Buff, preElementMainDamage);

        if (absorbed > 0)
            BroadcastHolyShieldChange(defenderState);

        var view = preElementMainDamage - absorbed + outcome.ElementDamage;
        var real = Math.Min(view, defenderState.Life);
        return (view, real);
    }

    private void RemoveDefenderHolyShields(PlayerRuntimeState defenderState)
    {
        if (HolyShieldResolver.RemoveAll(defenderState.Buffs.Buff))
            BroadcastHolyShieldChange(defenderState);
    }

    private void BroadcastHolyShieldChange(PlayerRuntimeState state)
    {
        var changed = state.BuffChangeScratch;
        Array.Clear(changed);
        changed[HolyShieldResolver.BaseSlot] = 1;
        foreach (var slot in HolyShieldResolver.TieredSlots)
            changed[slot] = 1;

        RecomputeStatsAndBroadcastBuffs(state, changed);
    }

    private enum CrossAvatarAttackKind
    {
        EnemyTribe,
        Duel
    }
}
