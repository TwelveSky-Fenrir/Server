using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class DungeonKeyUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<DungeonKeyUseItemHandler> logger) : IUseItemHandler
{
    public const int ItemId = DungeonAccessTicketResolver.DungeonKeyItemId;

    private const short DungeonKeyGrantEventCode = 31;

    private const byte SuccessOutcome = 1;

    private const short RequiredLevel = 100;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (state.Level < RequiredLevel)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Dungeon-Key (1048) rejected: level {Level} below {Required}",
                context.CharacterId, state.Level, RequiredLevel);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var resolved = DungeonAccessTicketResolver.Resolve(state.DungeonKeyTime,
            DungeonAccessTicketResolver.DungeonKeyAmount, context.Item.Quantity);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Dungeon-Key (1048) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(DungeonKeyGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"DungeonKeyTime={state.DungeonKeyTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, DungeonKeyTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Dungeon-Key mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Dungeon-Key (1048) applied: DungeonKeyTime {Old}->{New}",
            context.CharacterId, state.DungeonKeyTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }
}
