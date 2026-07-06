namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>Why an attack was refused before rolling -- each is a silent, packet-less <c>return;</c> in the legacy.</summary>
public enum AttackRejectReason
{
    None,
    SameCharacter,
    AttackerDead,
    DefenderDead,

    /// <summary>
    ///     Open-PvP (enemy-tribe) is disabled for the zone/map this attack is occurring in -- the
    ///     legacy-faithful gate at <c>AttackPlayer</c>'s ENEMY branch (S07_MyGame02.cpp:945-950). Duels are
    ///     never gated by this; it only applies to <see cref="CombatResolver.ResolveEnemyTribeAttack" />.
    /// </summary>
    ZonePvpDisabled,
    SameOrAlliedTribe,
    OutOfRange,
    AttackerProtected,
    DefenderProtected,
    AttackerHasNoAttackSuccess
}

/// <summary>A <see cref="Rejected" /> outcome carries no wire packet; a miss still echoes a zero-damage packet.</summary>
public readonly record struct AttackOutcome(
    bool Rejected,
    AttackRejectReason RejectReason,
    bool Hit,
    bool Critical,
    int DamageApplied,
    int ElementDamage,
    bool ChargeConsumed)
{
    public static AttackOutcome Reject(AttackRejectReason reason)
    {
        return new AttackOutcome(true, reason, false, false, 0, 0, false);
    }

    /// <summary>Charge buff is spent the moment an attack is attempted, win or miss -- callers with one must pass true.</summary>
    public static AttackOutcome Miss(bool chargeConsumed = false)
    {
        return new AttackOutcome(false, AttackRejectReason.None, false, false, 0, 0, chargeConsumed);
    }
}
