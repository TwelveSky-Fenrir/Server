namespace Fenrir.Application.Game.Domain.Consumables;

public static class PetExpBoostPillResolver
{
    public enum ChargeOutcome
    {
        Success,

                WouldExceedCeiling
    }

        public const int PerUnitAmount = 180;

        public static ChargeResult ResolveCharge(int currentPetExpX2Time, int bulkUnitCount)
    {
        var totalAmount = (long)PerUnitAmount * bulkUnitCount;
        var added = BankedCounterMath.AddWideSafe(currentPetExpX2Time, totalAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentPetExpX2Time);
    }

    public readonly record struct ChargeResult(ChargeOutcome Outcome, int NewCounterValue)
    {
        public bool Succeeded => Outcome == ChargeOutcome.Success;
    }
}
