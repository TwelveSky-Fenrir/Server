using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class ScrollOfSeekersUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<ScrollOfSeekersUseItemHandler> logger) : IUseItemHandler
{

        public static IEnumerable<int> HandledItemIds => ScrollOfSeekersResolver.HandledItemIds;

        private const short ScrollOfSeekersGrantEventCode = 33;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var resolved = ScrollOfSeekersResolver.Resolve(context.Item.ItemId, state.ScrollOfSeekersTime);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Scroll-of-Seekers ({ItemId}) rejected: {Outcome}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(ScrollOfSeekersGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, resolved.CreditedAmount,
            SuccessOutcome, $"ScrollOfSeekersTime={state.ScrollOfSeekersTime}->{resolved.NewZoneTime}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, ScrollOfSeekersTime: resolved.NewZoneTime),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Scroll-of-Seekers mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Scroll-of-Seekers ({ItemId}) applied: ScrollOfSeekersTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.ScrollOfSeekersTime, resolved.NewZoneTime);

        return UseItemResponses.Success(context.Page, context.Index);
    }
}
