using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 CP Ticket family (world.Items 691-694, 1444, 1447-1449, 1499, 8431-8434) -- a bulk-aware,
///     server-authoritative credit onto the character's Contribution Points balance
///     (<see cref="PlayerRuntimeState.ContributionPoints" />), clamped to on-hand stock and the shared
///     999-unit duplication ceiling via <see cref="CpTicketResolver" />. On success, credits the computed
///     total, consumes exactly the resolved bulk-use count, and echoes the new CP balance (Value) and the
///     units-consumed count (Value2) -- the one family in this op23 slice whose response documents Value2 at
///     all (every other family resets it to zero).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5267-5367 (CP Ticket family, full branch: bulk-use-count
///     computation, overflow guard, direct currency credit, bulk-count consumption -- see
///     <see cref="CpTicketResolver" /> for the per-item amount table and its own citations). Every rejection
///     reason (insufficient/unrecognized item, overflow) collapses to a clean Result=1 reply rather than a
///     disconnect, the same op23 simplification <see cref="TitleUpgradeUseItemHandler" />/
///     <see cref="PalaceRankUpgradeUseItemHandler" /> already use. Consumption is applied BEFORE the CP credit
///     is mirrored onto the zone -- same security ordering as those two handlers (and the Rebirth-Pill
///     branch): a dropped mirror can only lose the ticket stack, never grant unpaid CP.
/// </remarks>
public sealed class CpTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<CpTicketUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>
    ///     game.EventLog.EventCode for a CP-ticket redemption credit -- an app-owned value scoped within
    ///     <see cref="EventLogCategory.Currency" />, distinct from that category's GP-ticket-credit (1) and
    ///     Title-upgrade-spend (2) EventCodes.
    /// </summary>
    private const short CpTicketCreditEventCode = 3;

    private const byte SuccessOutcome = 1;

    /// <summary>Every world.Items id this family claims -- see <see cref="CpTicketResolver.HandledItemIds" />.</summary>
    public static IEnumerable<int> HandledItemIds => CpTicketResolver.HandledItemIds;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var resolved = CpTicketResolver.Resolve(context.Item.ItemId, context.Value, context.Item.Quantity,
            state.ContributionPoints);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 CP-ticket ({ItemId}) rejected: {Outcome} (quantity {Quantity})",
                context.CharacterId, context.Item.ItemId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(CpTicketCreditEventCode, EventLogCategory.Currency, context.AccountId,
            context.CharacterId, null, null, null, resolved.CreditedAmount, null, context.Item.ItemId,
            resolved.UnitsConsumed, SuccessOutcome,
            $"CP={state.ContributionPoints}->{resolved.NewContributionPoints}", cancellationToken);

        // Consume the ticket stack first (durable) so a dropped mirror can only lose it, never grant unpaid CP.
        var remaining = context.Item.Quantity - resolved.UnitsConsumed;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId,
                    resolved.NewContributionPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 CP-ticket mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 CP-ticket ({ItemId}) applied: {UnitsConsumed} unit(s) consumed, CP {OldCp}->{NewCp}",
            context.CharacterId, context.Item.ItemId, resolved.UnitsConsumed, state.ContributionPoints,
            resolved.NewContributionPoints);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewContributionPoints,
            resolved.UnitsConsumed);
    }
}
