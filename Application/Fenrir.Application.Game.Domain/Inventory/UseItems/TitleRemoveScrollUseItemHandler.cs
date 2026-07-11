using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 items 1200/8419/1494 -- the title-remove scroll. Fully clears the character's title (family and
///     rank together) and refunds the cumulative contribution-point (CP) cost of every rank below the one held
///     -- the full sum for items 1200/8419, 70% of it for item 1494. Distinct from, and never to be confused
///     with, <see cref="TitleUpgradeUseItemHandler" /> (item 891): that is a single-step Lv12-&gt;Lv13 gate on
///     its own fixed 10000-CP cost, unrelated to this scroll's per-rank cumulative-refund formula.
/// </summary>
/// <remarks>
///     Réf. C++ : "Title-remove-scroll consumer (op23 family — items 1200 / 8419 / 1494)" behavior contract
///     (legacy-behavior-translator), itself citing Server/ts25zone/S04_MyWork03.cpp:4654-4682 (the case block)
///     and Server/ts25zone/S07_MyGame03.cpp:9141-9163/:25-37 (<c>ReturnKillOtherTribeFromTitle</c>, now modeled
///     by <see cref="TitleContributionCost" />) throughout. Several deliberate translation decisions:
///     <list type="bullet">
///         <item>
///             <b>Consume-then-mirror ordering, diverging from the legacy's own consume-last sequence.</b> The
///             legacy source decrements the scroll's quantity LAST, after the CP refund and title clear have
///             already committed. This reorders to consume the scroll FIRST (durable), then mirror the CP
///             refund/title-clear/stat-recompute -- the same hardening
///             <see cref="TitleUpgradeUseItemHandler" />/<see cref="Inventory.UseItems.ForcedNeutralTribeResetUseItemHandler" />
///             already document on themselves ("a dropped mirror can only lose the [benefit], never grant an
///             unpaid one" -- here inverted: a dropped mirror can only lose the REFUND the player already paid
///             the scroll for, never let them keep the scroll AND get the refund). The audit-log entry itself
///             still logs first, matching the legacy's own explicit "recorded before the item is actually
///             consumed" ordering, since <see cref="IEventLogRepository.LogAsync" /> has no domain failure path
///             to protect against.
///         </item>
///         <item>
///             <b>Stack-safety classification (whole-stack vs. one-unit consumption) is unresolved data, not
///             invented here.</b> The originating contract's own open question confirms items 1200/1494/8419's
///             iSort classification cannot be recovered from Server/ C++ alone (seed data, not source). Reuses
///             <see cref="CashItemStackConsumption" /> -- the SAME shared, explicitly-flagged
///             "defaults to whole-stack consumption until item data confirms otherwise" placeholder this
///             codebase already applies to the OTHER item family (proxy-shop rental scrolls) with the identical
///             unresolved-classification gap, rather than a bespoke assumption invented for this handler alone.
///         </item>
///         <item>
///             <b>No broadcast to nearby players</b> -- faithfully not reproduced. The legacy source's own
///             notification call for this exact case is present but commented out (dead by comment, not by any
///             build-variant macro), in direct contrast to the separate title-acquisition item's own call site
///             a few lines away, which does broadcast. <see cref="TribeProgressZoneCommand.FullActionRebroadcast" />
///             is therefore left at its default <see langword="false" /> here -- unlike
///             <see cref="TitleUpgradeUseItemHandler" />, which sets it <see langword="true" /> as a stand-in
///             for a DIFFERENT legacy broadcast that DOES fire on that sibling item's own success path.
///         </item>
///         <item>
///             <b>The level-13-title zero-refund edge case is reproduced exactly, not "fixed."</b>
///             <see cref="TitleContributionCost.CumulativeRefund" /> already returns 0 for a title portion
///             outside 1-12 (LV13 is dead config in the shipped build) -- this handler never special-cases that
///             return value, so a (structurally unreachable in a live build, but defensively handled) level-13
///             title still fully loses its title and still consumes the scroll for a refund of exactly 0,
///             matching the legacy's own unchecked-return-value quirk precisely.
///         </item>
///         <item>
///             <b>The response's secondary value field</b> (Value2) is left at its default 0 -- the originating
///             contract's own open question about a shared, non-request-scoped legacy variable riding into that
///             field has no Fenrir equivalent to reproduce at all: this is a per-request architecture with no
///             such shared mutable state to begin with, not a gap.
///         </item>
///     </list>
/// </remarks>
public sealed class TitleRemoveScrollUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<TitleRemoveScrollUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1200/8419 (full refund) and 1494 (70% refund) -- see <see cref="TitleContributionCost.TryResolveRefundType" />.</summary>
    public static readonly ImmutableArray<int> HandledItemIds = [1200, 8419, 1494];

    /// <summary>
    ///     game.EventLog.EventCode for the title-remove CP refund -- an app-owned value scoped within
    ///     <see cref="EventLogCategory.Currency" />, distinct from that category's GP-ticket-credit (1),
    ///     title-upgrade-spend (2), and CP-ticket-credit (3) EventCodes.
    /// </summary>
    private const short TitleRemoveRefundEventCode = 4;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (state.Title == 0)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 title-remove ({ItemId}) rejected: no title held",
                context.CharacterId, context.Item.ItemId);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        // Registry routing already guarantees only 1200/8419/1494 ever reach this handler (HandledItemIds is
        // the same 3 ids), but this still defends against a future registry-wiring mistake rather than
        // assuming it.
        if (!TitleContributionCost.TryResolveRefundType(context.Item.ItemId, out var refundType))
        {
            logger.LogWarning(
                "Character {CharacterId} op23 title-remove: item {ItemId} is not a recognized title-remove scroll",
                context.CharacterId, context.Item.ItemId);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var oldTitle = state.Title;
        var oldContributionPoints = state.ContributionPoints;
        var refund = TitleContributionCost.CumulativeRefund(oldTitle, refundType);

        var overflow = BankedCounterMath.AddWideSafe(state.ContributionPoints, refund);
        if (!overflow.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 title-remove ({ItemId}) rejected: refund {Refund} would overflow CP ceiling from {Current}",
                context.CharacterId, context.Item.ItemId, refund, state.ContributionPoints);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var newContributionPoints = overflow.NewValue;
        var updatedStats = UseItemStatRecompute.WithTitleHalo(state, worldData, 0, state.Halo);

        // Hardening: an audit trail for an economically-significant action the legacy source itself never logs
        // (contrast the sibling GOD-scroll tribe-change equip-mutation helper, which does log every change it
        // makes) -- logged before consumption, matching the legacy's own log-then-consume ordering for this
        // one specific step.
        await eventLog.LogAsync(TitleRemoveRefundEventCode, EventLogCategory.Currency, context.AccountId,
            context.CharacterId, null, null, null, refund, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Title={oldTitle}->0", cancellationToken);

        // Consume the scroll first (durable) so a dropped mirror can only lose the refund the player already
        // paid the scroll for, never let them keep the scroll and still collect it later.
        var remaining = CashItemStackConsumption.RemainingQuantity(context.Item.ItemId, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, newContributionPoints, Title: 0,
                    UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 title-remove mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 title-remove ({ItemId}) applied: title {OldTitle}->0, CP {OldCp}->{NewCp} (refund {Refund})",
            context.CharacterId, context.Item.ItemId, oldTitle, oldContributionPoints, newContributionPoints,
            refund);

        return UseItemResponses.Success(context.Page, context.Index, newContributionPoints);
    }
}
