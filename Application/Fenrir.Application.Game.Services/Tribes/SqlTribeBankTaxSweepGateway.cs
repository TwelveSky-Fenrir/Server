using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     The real, SQL-backed <see cref="ITribeBankTaxSweepGateway" /> that replaces
///     <see cref="LoggingOnlyTribeBankTaxSweepGateway" />: persists one zone's already-detached 10-minute
///     tribe-bank income-tax sweep into the durable 50-slot-per-tribe grid via
///     <see cref="ITribeBankSweepRepository" /> -&gt; game.usp_TribeBank_ApplyTaxSweep (the scan-for-room /
///     cap-fallback merge that game.usp_TribeBank_Deposit never provided -- see
///     <see cref="ITribeBankTaxSweepGateway" />'s own remarks).
/// </summary>
/// <remarks>
///     The zone-local accumulator was already zeroed by <see cref="TribeBankTaxAccumulator.TrySweep" /> before
///     this is called, so a thrown persistence failure here loses that window's entire tax with no retry --
///     the correct, contract-specified fire-and-forget failure direction. <see cref="TribeBankTaxSweepFlushHost" />
///     catches and logs that exception. An all-zero payload never needs a round trip (the procedure would just
///     no-op), so it is short-circuited here.
///     <para>
///         <paramref name="mapId" /> is intentionally unused: the durable tribe-bank grid is a single global
///         store (legacy TRIBE_BANK_INFO.DAT on the one PlayUser process), not per-map -- every zone's sweep
///         merges into the same four-tribe grid.
///     </para>
/// </remarks>
public sealed class SqlTribeBankTaxSweepGateway(ITribeBankSweepRepository sweeps) : ITribeBankTaxSweepGateway
{
    public async Task SweepAsync(short mapId, TribeBankTaxSweepPayload payload, CancellationToken ct)
    {
        if (payload.IsEmpty)
            return;

        await sweeps.ApplyTaxSweepAsync(payload.Tribe0, payload.Tribe1, payload.Tribe2, payload.Tribe3, ct)
            .ConfigureAwait(false);
    }
}
