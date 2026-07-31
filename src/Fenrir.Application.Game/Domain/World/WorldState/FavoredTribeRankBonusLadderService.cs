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

        await ApplyAsync(favoredTribeId, "pending flag", ct).ConfigureAwait(false);
    }

    // Rotation hebdomadaire de la tribu avantagee. Le seul site d'appel legacy est COMMENTE, dans le bloc
    // "7 jours ecoules" du rollover de rang heros (Server/ts25center/S08_MyDB.cpp:446) : on le retablit
    // exactement la. La formule, elle, est de la parite stricte (S08_MyDB.cpp:144-184).
    // Le declencheur (sentinelle 7 jours) est deja consomme quand on arrive ici : si la persistance des
    // totaux echoue APRES avoir avance HighTribe, le flush ecrit quand meme HighTribe et l'echelle reste
    // perimee pendant sept jours. On avance donc HighTribe seulement apres succes complet.
    public async Task RotateToNextFavoredTribeAsync(CancellationToken ct)
    {
        var next = FavoredTribeRankBonusLadder.NextFavoredTribe(worldState.World.HighTribe);

        if (!await ApplyAsync(next, "hero-rank rollover", ct).ConfigureAwait(false))
            return;

        worldState.SetHighTribe(next);
        logger.LogInformation("Advantaged tribe rotated to {NextTribe} on hero-rank rollover", next);
    }

    private async Task<bool> ApplyAsync(byte favoredTribeId, string trigger, CancellationToken ct)
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(favoredTribeId);

        if (!await worldState.TryOverwriteTribePointTotalsAsync(totals, ct).ConfigureAwait(false))
        {
            logger.LogError(
                "FavoredTribeRankBonusLadder: totals persist failed after the {Trigger} already fired -- this request is now permanently lost, no broadcast",
                trigger);
            return false;
        }

        broadcaster.AnnounceTribePointTotals(totals);

        logger.LogInformation(
            "FavoredTribeRankBonusLadder: applied and broadcast ({Trigger}) -- favored tribe {FavoredTribeId}, totals Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            trigger, favoredTribeId, totals[0], totals[1], totals[2], totals[3]);

        return true;
    }
}
