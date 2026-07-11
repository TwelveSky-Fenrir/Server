using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class TitleUpgradeUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<TitleUpgradeUseItemHandler> logger) : IUseItemHandler
{
    public const int ItemId = 891;

    private const int RequiredTitleLevel = 12;
    private const int TitleLevelModulus = 100;
    private const int RequiredContributionPoints = 10000;

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
