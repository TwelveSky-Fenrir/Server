using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.Tribes;

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
