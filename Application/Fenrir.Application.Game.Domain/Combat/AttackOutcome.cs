namespace Fenrir.Application.Game.Domain.Combat;

public enum AttackRejectReason
{
    None,
    SameCharacter,
    AttackerDead,
    DefenderDead,

        ZonePvpDisabled,

        SameOrAlliedTribe,
    OutOfRange,
    AttackerProtected,
    DefenderProtected,
    AttackerHasNoAttackSuccess,

        DuelNotAuthorized,

        DefenderShopOpen,

        DefenderActionStateBlocksTargeting,

        NewbieProtectionLevelGap,

        InvalidAttackModeSelector,

        AntiCheatEchoMismatch,

        OwnerNameLocked
}

public readonly record struct AttackOutcome(
    bool Rejected,
    AttackRejectReason RejectReason,
    bool Hit,
    bool Critical,
    int DamageApplied,
    int ViewDamage,
    int ElementDamage,
    bool ChargeConsumed)
{
    public static AttackOutcome Reject(AttackRejectReason reason)
    {
        return new AttackOutcome(true, reason, false, false, 0, 0, 0, false);
    }

        public static AttackOutcome Miss(bool chargeConsumed = false)
    {
        return new AttackOutcome(false, AttackRejectReason.None, false, false, 0, 0, 0, chargeConsumed);
    }
}
