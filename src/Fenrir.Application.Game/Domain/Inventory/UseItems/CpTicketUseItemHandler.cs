using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class CpTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<CpTicketUseItemHandler> logger) : IUseItemHandler
{
    private const short CpTicketCreditEventCode = 3;

    private const byte SuccessOutcome = 1;

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
