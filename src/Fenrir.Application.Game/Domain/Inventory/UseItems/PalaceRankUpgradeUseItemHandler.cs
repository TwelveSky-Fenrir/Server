using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class PalaceRankUpgradeUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<PalaceRankUpgradeUseItemHandler> logger) : IUseItemHandler
{
    public const int ItemIdPlus1 = 2193;
    public const int ItemIdPlus10 = 867;

    public static IEnumerable<int> HandledItemIds { get; } = [ItemIdPlus1, ItemIdPlus10];

    private const int RankCeiling = 96;

    private const short PalaceRankGrantEventCode = 5;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Item.Quantity < 1 || state.Halo >= RankCeiling)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 palace-rank ({ItemId}) rejected: rank {Rank}, quantity {Quantity}",
                context.CharacterId, context.Item.ItemId, state.Halo, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var grantAmount = context.Item.ItemId == ItemIdPlus10 ? 10 : 1;
        var newRank = Math.Min(state.Halo + grantAmount, RankCeiling);
        var updatedStats = UseItemStatRecompute.WithTitleHalo(state, worldData, state.Title, newRank);

        await eventLog.LogAsync(PalaceRankGrantEventCode, EventLogCategory.CashItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Rank={state.Halo}->{newRank}", cancellationToken);

        var remaining =
            CashItemStackConsumption.RemainingQuantity(context.Definition.Item.Sort, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Halo: newRank, UpdatedStats: updatedStats,
                    FullActionRebroadcast: true), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 palace-rank mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        if (newRank == RankCeiling)
            logger.LogInformation(
                "Character {CharacterId} reached palace rank {RankCeiling}: milestone announcement not modeled (confirmed dead end in legacy -- no notice text/send path exists to translate)",
                context.CharacterId, RankCeiling);

        logger.LogInformation("Character {CharacterId} op23 palace-rank (2193) applied: rank {OldRank}->{NewRank}",
            context.CharacterId, state.Halo, newRank);

        return UseItemResponses.Success(context.Page, context.Index);
    }
}
