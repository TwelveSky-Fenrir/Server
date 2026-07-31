using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public interface ITribeBankTaxSweepGateway
{
    public Task SweepAsync(short mapId, TribeBankTaxSweepPayload payload, CancellationToken ct);
}

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
