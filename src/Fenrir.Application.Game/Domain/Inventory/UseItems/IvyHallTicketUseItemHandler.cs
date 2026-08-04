using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class IvyHallTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<IvyHallTicketUseItemHandler> logger) : IUseItemHandler
{
    private const short IvyHallTicketGrantEventCode = 32;

    private const byte SuccessOutcome = 1;

    private const int IvyHallTicketCeiling = 1_576_800;

    public static IEnumerable<int> HandledItemIds { get; } =
    [
        DungeonAccessTicketResolver.IvyHallTicketSmallItemId,
        DungeonAccessTicketResolver.IvyHallTicketLargeItemId
    ];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var amount = AmountFor(context.Item.ItemId);
        var resolved = DungeonAccessTicketResolver.Resolve(state.IvyHallTicketTime, amount, context.Item.Quantity,
            IvyHallTicketCeiling);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Ivy-Hall-Ticket ({ItemId}) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(IvyHallTicketGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"IvyHallTicketTime={state.IvyHallTicketTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, IvyHallTicketTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Ivy-Hall-Ticket mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Ivy-Hall-Ticket ({ItemId}) applied: IvyHallTicketTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.IvyHallTicketTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }

    private static int AmountFor(int itemId)
    {
        return itemId switch
        {
            DungeonAccessTicketResolver.IvyHallTicketSmallItemId => DungeonAccessTicketResolver
                .IvyHallTicketSmallAmount,
            DungeonAccessTicketResolver.IvyHallTicketLargeItemId => DungeonAccessTicketResolver
                .IvyHallTicketLargeAmount,
            _ => 0
        };
    }
}
