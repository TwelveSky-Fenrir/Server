using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 item 891 — the Title Lv12→Lv13 ticket. Advances the character's title by exactly one rank, and
///     only from title-level 12 to 13: it is a single-step gate, not a general title incrementer. Gated on the
///     current title level (title value modulo 100) being exactly 12 and the character's cross-tribe-kill count
///     (Contribution Points) being at least 10000; on success it deducts exactly 10000 CP, increments the
///     title, recomputes equipment stats + HP/MP, consumes the item, and refreshes the addressed slot and the
///     surrounding avatar view. Server-authoritative throughout: the client's supplied value is never a gate,
///     and the 10000-CP cost is enforced server-side.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4562-4652 (the item-891 case: exact-level-12 and
///     CP&gt;=10000 gate, the 10000-CP deduct, title+1, equipment/HP-MP recompute, the consume rule, the direct
///     inventory refresh, and the title-change broadcast — legacy deliberately omits the plain success result
///     here). Two divergences from that source, both deliberate: (1) legacy sends no success result and lets a
///     direct slot refresh + a change-info-15 title broadcast stand in; Fenrir has no "refresh instead of
///     result" wire seam through the op23 service, so this returns a plain result-0 alongside the same durable
///     refresh + a full avatar-action rebroadcast (the change-info-15 title-specific push is flagged, not
///     modeled — no dedicated broadcast field exists on <see cref="TribeProgressZoneCommand" />). (2) The item
///     is consumed BEFORE the CP-deduct/title-grant is mirrored, so a dropped mirror can only lose the ticket,
///     never grant an unpaid title — the same security ordering the Rebirth-Pill branch uses.
/// </remarks>
public sealed class TitleUpgradeUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<TitleUpgradeUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 891 — the Title Lv12→Lv13 ticket.</summary>
    public const int ItemId = 891;

    private const int RequiredTitleLevel = 12;
    private const int TitleLevelModulus = 100;
    private const int RequiredContributionPoints = 10000;

    /// <summary>
    ///     game.EventLog.EventCode for the 10000-CP title-upgrade spend — an app-owned value scoped within
    ///     <see cref="EventLogCategory.Currency" />, distinct from that category's GP-ticket-credit EventCode 1.
    ///     The CP delta rides <see cref="IEventLogRepository.LogAsync" />'s <c>deltaMoney</c> parameter (there is
    ///     no dedicated Contribution-Points column on game.EventLog), the same convention
    ///     <see cref="EventLogCategory.MountAttribute" /> already documents. A future central event-code
    ///     registry should reconcile this constant rather than reuse its numeric value elsewhere.
    /// </summary>
    private const short TitleUpgradeSpendEventCode = 2;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Item.Quantity < 1 ||
            state.Title % TitleLevelModulus != RequiredTitleLevel ||
            state.ContributionPoints < RequiredContributionPoints)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 title-upgrade (891) rejected: title {Title}, CP {Cp}, quantity {Quantity}",
                context.CharacterId, state.Title, state.ContributionPoints, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var newTitle = state.Title + 1;
        var newContributionPoints = state.ContributionPoints - RequiredContributionPoints;
        var updatedStats = UseItemStatRecompute.WithTitleHalo(state, worldData, newTitle, state.Halo);

        await eventLog.LogAsync(TitleUpgradeSpendEventCode, EventLogCategory.Currency, context.AccountId,
            context.CharacterId, null, null, null, -RequiredContributionPoints, null, context.Item.ItemId,
            context.Item.Quantity, SuccessOutcome, $"Title={state.Title}->{newTitle}", cancellationToken);

        // Consume the ticket first (durable) so a dropped mirror can only lose it, never grant an unpaid title.
        var remaining = CashItemStackConsumption.RemainingQuantity(context.Item.ItemId, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, newContributionPoints,
                    Title: newTitle, UpdatedStats: updatedStats, FullActionRebroadcast: true), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 title-upgrade mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 title-upgrade (891) applied: title {OldTitle}->{NewTitle}, CP {NewCp}",
            context.CharacterId, state.Title, newTitle, newContributionPoints);

        return UseItemResponses.Success(context.Page, context.Index);
    }
}
