using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class EliteDungeonTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<EliteDungeonTicketUseItemHandler> logger) : IUseItemHandler
{
    private const short EliteDungeonTicketGrantEventCode = 30;

    private const byte SuccessOutcome = 1;

    public static IEnumerable<int> HandledItemIds { get; } =
    [
        DungeonAccessTicketResolver.EliteDungeonTicketLargeItemId,
        DungeonAccessTicketResolver.EliteDungeonTicketMediumItemId,
        DungeonAccessTicketResolver.EliteDungeonTicketSmallItemId
    ];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var amount = AmountFor(context.Item.ItemId);
        var resolved = DungeonAccessTicketResolver.Resolve(state.EliteDungeonTime, amount, context.Item.Quantity);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Elite-Dungeon-Ticket ({ItemId}) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(EliteDungeonTicketGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"EliteDungeonTime={state.EliteDungeonTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, EliteDungeonTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Elite-Dungeon-Ticket mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Elite-Dungeon-Ticket ({ItemId}) applied: EliteDungeonTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.EliteDungeonTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }

    private static int AmountFor(int itemId)
    {
        return itemId switch
        {
            DungeonAccessTicketResolver.EliteDungeonTicketLargeItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketLargeAmount,
            DungeonAccessTicketResolver.EliteDungeonTicketMediumItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketMediumAmount,
            DungeonAccessTicketResolver.EliteDungeonTicketSmallItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketSmallAmount,
            _ => 0
        };
    }
}
