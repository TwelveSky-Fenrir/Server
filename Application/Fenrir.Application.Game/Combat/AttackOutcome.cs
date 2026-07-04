namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Why <see cref="CombatResolver" /> refused to even roll an attack -- every value is a silent, packet-less
///     <c>return;</c> in the legacy (report 05 §4 point 1's guard list).
/// </summary>
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

/// <summary>
///     The fully-resolved result of one <see cref="CombatResolver" /> call -- everything <c>Zone.ApplyCombatCommand</c>
///     needs to mutate HP, broadcast <c>AttackResponse</c>, and decide whether to call <c>Zone.ApplyDeath</c>.
///     A <see cref="Rejected" /> outcome carries no wire packet at all (the legacy's own bare <c>return;</c>
///     guards, report 05 §4 point 1); a non-rejected-but-missed attack (<see cref="Hit" /> false) still gets a
///     wire echo (<c>AttackResultValue = 0</c>) but zero damage.
/// </summary>
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

    /// <summary>
    ///     <paramref name="chargeConsumed" /> defaults to false but callers with an attacker-side charge buff
    ///     must pass true: the legacy spends the charge buff the moment an attack is ATTEMPTED (<c>AttackPlayer</c>,
    ///     <c>S07_MyGame02.cpp:995-999</c>, unconditional <c>DecreaseBuff(8,...)</c> BEFORE the hit-chance roll at
    ///     <c>:1043-1051</c>), win or miss -- not only on a hit.
    /// </summary>
    public static AttackOutcome Miss(bool chargeConsumed = false)
    {
        return new AttackOutcome(false, AttackRejectReason.None, false, false, 0, 0, chargeConsumed);
    }
}
