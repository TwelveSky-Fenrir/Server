using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class TitleRemoveScrollUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<TitleRemoveScrollUseItemHandler> logger) : IUseItemHandler
{
    private const short TitleRemoveRefundEventCode = 4;

    private const byte SuccessOutcome = 1;

    public static readonly ImmutableArray<int> HandledItemIds = [1200, 8419, 1494];

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

        await eventLog.LogAsync(TitleRemoveRefundEventCode, EventLogCategory.Currency, context.AccountId,
            context.CharacterId, null, null, null, refund, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Title={oldTitle}->0", cancellationToken);

        var remaining =
            CashItemStackConsumption.RemainingQuantity(context.Definition.Item.Sort, context.Item.Quantity);
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
