using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 item 2193 — the Palace Rank +1 ticket. Raises the character's halo/palace-rank by one, capped at
///     96: the rank cannot be pushed past that ceiling. On success it increments the rank, recomputes
///     equipment stats + HP/MP, consumes the item, refreshes the rank/halo view, and rebroadcasts the avatar
///     to surrounding players; reaching 96 exactly additionally fires a server-wide milestone announcement.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:6912-7008 (the item-2193 case: rank&lt;96 gate, rank+1,
///     equipment/HP-MP recompute, rank/halo UI refresh, the milestone-96 center-server announcement, the
///     consume rule, the plain success result, and the surrounding-player rebroadcast). "Halo/palace rank" is
///     modeled as <see cref="PlayerRuntimeState.Halo" /> itself — the same field StatCalculator reads as aHalo,
///     and the same one <c>UseInventoryItemService</c>'s CP-Prot-Charm branch already treats as the rank value.
///     Two deliberate divergences: (1) the milestone-96 center-server announcement has no Fenrir cross-process
///     broadcast seam in this workstream's scope — it is flagged (logged) and left for a follow-up, not
///     invented; the rank itself is still raised and persisted. (2) The item is consumed BEFORE the rank grant
///     is mirrored, so a dropped mirror can only lose the ticket, never grant a free rank — the same security
///     ordering the Rebirth-Pill branch uses, and materially important here since (unlike the single-step
///     title ticket) the rank gate re-opens after each successful use.
/// </remarks>
public sealed class PalaceRankUpgradeUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<PalaceRankUpgradeUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 2193 — the Palace Rank +1 ticket.</summary>
    public const int ItemId = 2193;

    /// <summary>The rank ceiling: the ticket cannot raise the rank at or above this value.</summary>
    private const int RankCeiling = 96;

    /// <summary>
    ///     game.EventLog.EventCode for a palace-rank grant — an app-owned value scoped within
    ///     <see cref="EventLogCategory.CashItemUse" />, distinct from that category's proxy-shop (2) and
    ///     pet-EXP-boost (3) EventCodes. A future central event-code registry should reconcile this constant.
    /// </summary>
    private const short PalaceRankGrantEventCode = 5;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Item.Quantity < 1 || state.Halo >= RankCeiling)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 palace-rank (2193) rejected: rank {Rank}, quantity {Quantity}",
                context.CharacterId, state.Halo, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var newRank = state.Halo + 1;
        var updatedStats = UseItemStatRecompute.WithTitleHalo(state, worldData, state.Title, newRank);

        await eventLog.LogAsync(PalaceRankGrantEventCode, EventLogCategory.CashItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Rank={state.Halo}->{newRank}", cancellationToken);

        // Consume the ticket first (durable) so a dropped mirror can only lose it, never grant a free rank.
        var remaining = CashItemStackConsumption.RemainingQuantity(context.Item.ItemId, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Halo: newRank, UpdatedStats: updatedStats,
                    FullActionRebroadcast: true), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 palace-rank mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        if (newRank == RankCeiling)
            // TODO(C9): the legacy fires a server-wide milestone announcement to the center server when the
            // rank reaches 96 (S04_MyWork03.cpp:6912-7008). No Fenrir cross-process center-broadcast seam is in
            // this workstream's scope; the rank is still raised and persisted. Flagged for a follow-up finding.
            logger.LogInformation(
                "Character {CharacterId} reached palace rank {RankCeiling}: center-server milestone announcement not modeled (deferred)",
                context.CharacterId, RankCeiling);

        logger.LogInformation("Character {CharacterId} op23 palace-rank (2193) applied: rank {OldRank}->{NewRank}",
            context.CharacterId, state.Halo, newRank);

        return UseItemResponses.Success(context.Page, context.Index);
    }
}
