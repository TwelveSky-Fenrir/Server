namespace Fenrir.Data.Abstractions.Tribes;

/// <summary>
///     The durable 10-minute tribe-bank income-tax sweep merge (C17 side effect A3). Kept separate from
///     <see cref="ITribeRepository" />'s single-slot deposit/withdraw surface because the sweep is a whole-grid
///     scan-for-room / cap-fallback merge over all four tribes' 50 slots at once, not a single-slot movement.
/// </summary>
public interface ITribeBankSweepRepository
{
    /// <summary>
    ///     Merges one sweep's four per-tribe incoming amounts into the persistent 50-slot-per-tribe bank grid
    ///     (game.usp_TribeBank_ApplyTaxSweep). Per tribe with a positive amount, the whole amount is deposited
    ///     into the first slot that can absorb it under the 2,000,000,000 ceiling; if every slot would
    ///     overflow, the first sub-ceiling slot is clamped up to the ceiling; if every slot is already at the
    ///     ceiling, the amount is dropped silently. A zero/negative per-tribe amount is skipped. Best-effort by
    ///     contract: money that cannot be placed is lost, never an error -- the procedure never throws a domain
    ///     error, so this method only surfaces genuine infrastructure faults.
    /// </summary>
    public ValueTask ApplyTaxSweepAsync(long tribe0Amount, long tribe1Amount, long tribe2Amount,
        long tribe3Amount, CancellationToken ct);
}
