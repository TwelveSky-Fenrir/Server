using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     The persistent-side half of the tribe-bank tax sweep (contract side effect 4): receives one zone's
///     already-detached <see cref="TribeBankTaxSweepPayload" /> -- <see cref="TribeBankTaxAccumulator.TrySweep" />
///     has already zeroed the zone-local copy by the time this is called, so nothing here can ever recover a
///     dropped payload.
/// </summary>
/// <remarks>
///     This is the seam: the real implementation must, per tribe with a nonzero incoming amount, scan that
///     tribe's up-to-50 persistent bank slots in order for the first one where adding the incoming amount would
///     not exceed the 2,000,000,000 ceiling, add the full amount there (or force-set the first sub-ceiling slot
///     to exactly the ceiling if every slot would overflow, or drop the amount entirely if every slot is already
///     at the ceiling), then synchronously rewrite the whole persistent tribe-bank data set. That merge/rewrite
///     needs a new stored procedure -- the existing <c>game.usp_TribeBank_Deposit</c> only adds to one caller-
///     chosen slot with no ceiling check and no scan-for-room fallback, so it cannot implement this contract as
///     written. Designing that procedure (and the <c>ITribeRepository</c> method that would call it) is
///     <c>fenrir-database-engineer</c> work, not done in this batch -- see
///     <see cref="LoggingOnlyTribeBankTaxSweepGateway" /> for the safe placeholder wired in until then.
/// </remarks>
public interface ITribeBankTaxSweepGateway
{
    Task SweepAsync(short mapId, TribeBankTaxSweepPayload payload, CancellationToken ct);
}

/// <summary>
///     Logs every nonzero sweep instead of persisting it -- the safe placeholder until the real slot-scan/cap-
///     fallback merge procedure (see <see cref="ITribeBankTaxSweepGateway" />'s own remarks) exists. Wiring this
///     in production means <see cref="TribeBankTaxAccumulator" />/<see cref="Zone" />'s own accumulate-and-sweep
///     sequencing runs correctly (zone-local totals accrue and reset on the right 10-minute cadence), but no
///     swept amount is actually persisted yet -- exactly the same posture as
///     <c>LoggingOnlyHolyStoneCaptureRewardGateway</c> for its own not-yet-wired reward seam.
/// </summary>
public sealed class LoggingOnlyTribeBankTaxSweepGateway(ILogger<LoggingOnlyTribeBankTaxSweepGateway> logger)
    : ITribeBankTaxSweepGateway
{
    public Task SweepAsync(short mapId, TribeBankTaxSweepPayload payload, CancellationToken ct)
    {
        if (!payload.IsEmpty)
            logger.LogWarning(
                "Zone {MapId}: tribe bank tax sweep due (Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}) but no persistent sweep gateway is wired yet -- swept amount is discarded",
                mapId, payload.Tribe0, payload.Tribe1, payload.Tribe2, payload.Tribe3);

        return Task.CompletedTask;
    }
}
