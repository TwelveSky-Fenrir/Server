namespace Fenrir.Application.Game.Domain.Consumables;

public static class ProtectionChargeResolver
{
    public enum ChargeOutcome
    {
        Success,

        WouldExceedCeiling,

        HaloRankTooHigh
    }

    public const int HaloRankGateThreshold = 96;

    public static ChargeResult ResolveCharmCharge(int currentCounter, int perUnitAmount, int bulkUnitCount)
    {
        var totalAmount = (long)perUnitAmount * bulkUnitCount;
        var added = BankedCounterMath.AddWideSafe(currentCounter, totalAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue, bulkUnitCount)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentCounter, 0);
    }

    public static ChargeResult ResolveCpProtCharmCharge(int currentCounter, int perUnitAmount, int bulkUnitCount,
        int haloRank)
    {
        return haloRank >= HaloRankGateThreshold
            ? new ChargeResult(ChargeOutcome.HaloRankTooHigh, currentCounter, 0)
            : ResolveCharmCharge(currentCounter, perUnitAmount, bulkUnitCount);
    }

    public static ChargeResult ResolveScrollCharge(int currentCounter, int fixedAmount)
    {
        var added = BankedCounterMath.AddNarrow(currentCounter, fixedAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue, 1)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentCounter, 0);
    }

    public readonly record struct ChargeResult(ChargeOutcome Outcome, int NewCounterValue, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == ChargeOutcome.Success;
    }
}
