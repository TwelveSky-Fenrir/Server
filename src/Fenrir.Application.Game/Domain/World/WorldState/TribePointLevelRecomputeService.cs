using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public sealed class TribePointLevelRecomputeService(
    ITribePointRosterGateway rosterGateway,
    WorldStateService worldState,
    ILogger<TribePointLevelRecomputeService> logger)
{
    public async Task RecomputeAsync(CancellationToken ct)
    {
        IReadOnlyList<TribeRosterCharacterSnapshot>? roster;
        try
        {
            roster = await rosterGateway.GetRosterAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TribePointLevelRecompute: roster read threw -- previous totals left unchanged for this run");
            return;
        }

        if (roster is null)
        {
            logger.LogDebug(
                "TribePointLevelRecompute: roster unavailable -- previous totals left unchanged for this run");
            return;
        }

        var totals = TribePointLevelRecompute.ComputeTotals(roster);
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            worldState.SetTribePoints(tribeId, totals[tribeId]);

        logger.LogInformation(
            "TribePointLevelRecompute: totals overwritten -- Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            totals[0], totals[1], totals[2], totals[3]);
    }
}
