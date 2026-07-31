using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public sealed class FavoredTribeRankBonusLadderService(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ILogger<FavoredTribeRankBonusLadderService> logger,
    IWorldEventUplink? uplink = null)
{
    public const short PendingFlagValue = 1;

    public const short ConsumedFlagValue = 2;

    public const int TribePointTotalsEventCode = 1234;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task TickIfPendingAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (!await worldState.TryConsumeUpdateTribePointFlagAsync(PendingFlagValue, ConsumedFlagValue, ct)
                    .ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                return;
            }

            if (worldState.World.HighTribe is not { } favoredTribeId)
            {
                logger.LogWarning(
                    "FavoredTribeRankBonusLadder: pending flag consumed but no favored tribe is recorded (HighTribe is null) -- skipping this run, previous totals left unchanged, no broadcast");
                return;
            }

            await ApplyAsync(favoredTribeId, "pending flag", ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RotateToNextFavoredTribeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var next = FavoredTribeRankBonusLadder.NextFavoredTribe(worldState.World.HighTribe);

            if (!await ApplyAsync(next, "hero-rank rollover", ct).ConfigureAwait(false))
                return;

            worldState.SetHighTribe(next);
            logger.LogInformation("Advantaged tribe rotated to {NextTribe} on hero-rank rollover", next);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> ApplyAsync(byte favoredTribeId, string trigger, CancellationToken ct)
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(favoredTribeId);

        if (!await worldState.TryOverwriteTribePointTotalsAsync(totals, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            logger.LogError(
                "FavoredTribeRankBonusLadder: totals persist failed after the {Trigger} already fired -- this request is now permanently lost, no broadcast",
                trigger);
            return false;
        }

        broadcaster.AnnounceTribePointTotals(totals);
        PublishToOtherShards(totals);

        logger.LogInformation(
            "FavoredTribeRankBonusLadder: applied and broadcast ({Trigger}) -- favored tribe {FavoredTribeId}, totals Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            trigger, favoredTribeId, totals[0], totals[1], totals[2], totals[3]);

        return true;
    }

    private void PublishToOtherShards(IReadOnlyList<int> totals)
    {
        if (uplink is null)
            return;

        Span<byte> data = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        data.Clear();

        for (var i = 0; i < totals.Count; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data[(i * 4)..], totals[i]);

        uplink.Publish(TribePointTotalsEventCode, data);
    }
}
