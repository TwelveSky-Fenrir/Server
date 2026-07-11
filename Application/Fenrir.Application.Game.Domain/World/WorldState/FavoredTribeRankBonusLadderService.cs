using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public sealed class FavoredTribeRankBonusLadderService(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ILogger<FavoredTribeRankBonusLadderService> logger)
{
    public const short PendingFlagValue = 1;

    public const short ConsumedFlagValue = 2;

    public async Task TickIfPendingAsync(CancellationToken ct)
    {
        if (!await worldState.TryConsumeUpdateTribePointFlagAsync(PendingFlagValue, ConsumedFlagValue, ct)
                .ConfigureAwait(false))
            return;

        if (worldState.World.HighTribe is not { } favoredTribeId)
        {
            logger.LogWarning(
                "FavoredTribeRankBonusLadder: pending flag consumed but no favored tribe is recorded (HighTribe is null) -- skipping this run, previous totals left unchanged, no broadcast");
            return;
        }

        var totals = FavoredTribeRankBonusLadder.ComputeTotals(favoredTribeId);

        if (!await worldState.TryOverwriteTribePointTotalsAsync(totals, ct).ConfigureAwait(false))
        {
            logger.LogError(
                "FavoredTribeRankBonusLadder: totals persist failed after the flag was already consumed -- this request is now permanently lost, no broadcast");
            return;
        }

        broadcaster.AnnounceTribePointTotals(totals);

        logger.LogInformation(
            "FavoredTribeRankBonusLadder: applied and broadcast -- favored tribe {FavoredTribeId}, totals Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            favoredTribeId, totals[0], totals[1], totals[2], totals[3]);
    }
}
