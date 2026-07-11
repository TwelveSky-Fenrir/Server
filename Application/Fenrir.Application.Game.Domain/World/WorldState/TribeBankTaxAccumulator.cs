namespace Fenrir.Application.Game.Domain.World.WorldState;

public readonly record struct TribeBankTaxSweepPayload(long Tribe0, long Tribe1, long Tribe2, long Tribe3)
{
    public static readonly TribeBankTaxSweepPayload Empty = default;

    public bool IsEmpty => Tribe0 == 0 && Tribe1 == 0 && Tribe2 == 0 && Tribe3 == 0;

    public long this[byte tribeId] => tribeId switch
    {
        0 => Tribe0,
        1 => Tribe1,
        2 => Tribe2,
        3 => Tribe3,
        _ => throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
            $"TribeId must be 0-{TribeBankTaxAccumulator.TribeCount - 1}.")
    };
}

public sealed class TribeBankTaxAccumulator(Func<byte, byte>? resolveBeneficiaryTribe = null)
{
    public const int TribeCount = 4;

    public const long ZoneLocalCeiling = 2_000_000_000L;

    public const double NpcServiceTaxRate = 0.01;

    public const double MonsterKillCurrencyTaxRate = 0.09;

    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private readonly long[] _totals = new long[TribeCount];
    private TimeSpan _lastSweepAt = TimeSpan.Zero;

    public long GetTotal(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _totals[tribeId];
        }
    }

    public void CreditNpcServiceTax(byte payingTribe, int cost)
    {
        Credit(payingTribe, cost, NpcServiceTaxRate);
    }

    public void CreditMonsterKillCurrencyTax(byte killerTribe, long postReductionAmount)
    {
        Credit(killerTribe, postReductionAmount, MonsterKillCurrencyTaxRate);
    }

    private void Credit(byte actingTribe, long baseAmount, double rate)
    {
        if (actingTribe >= TribeCount)
            return;

        var beneficiary = resolveBeneficiaryTribe?.Invoke(actingTribe) ?? actingTribe;
        if (beneficiary >= TribeCount)
            return;

        var tax = (long)(baseAmount * rate);
        if (tax <= 0)
            return;

        lock (_lock)
        {
            if (_totals[beneficiary] + tax > ZoneLocalCeiling)
                return;

            _totals[beneficiary] += tax;
        }
    }

    public bool TrySweep(TimeSpan zoneUptimeNow, out TribeBankTaxSweepPayload payload)
    {
        lock (_lock)
        {
            if (zoneUptimeNow - _lastSweepAt < SweepInterval)
            {
                payload = TribeBankTaxSweepPayload.Empty;
                return false;
            }

            payload = new TribeBankTaxSweepPayload(_totals[0], _totals[1], _totals[2], _totals[3]);
            Array.Clear(_totals);
            _lastSweepAt = zoneUptimeNow;
            return true;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}
