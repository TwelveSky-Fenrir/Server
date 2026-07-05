namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>Why an attack was refused before rolling -- each is a silent, packet-less <c>return;</c> in the legacy.</summary>
public enum AttackRejectReason
{
    None,
    SameCharacter,
    AttackerDead,
    DefenderDead,
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
