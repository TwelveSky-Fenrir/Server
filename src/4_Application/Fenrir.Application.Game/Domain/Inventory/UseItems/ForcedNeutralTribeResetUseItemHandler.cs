using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class ForcedNeutralTribeResetUseItemHandler(
    ICharacterRepository characters,
    UseItemInventoryWriter inventoryWriter,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    GameServerOptions options,
    ILogger<ForcedNeutralTribeResetUseItemHandler> logger) : IUseItemHandler
{
    public const int ItemId = 8100;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Item.Quantity < 1)
            return UseItemResponses.Fail(context.Page, context.Index);

        var neutralHomeZoneOnline = await IsNeutralHomeZoneOnlineAsync(cancellationToken);

        var outcome = ForcedNeutralTribeResetGate.Evaluate(new ForcedNeutralTribeResetEligibilityContext(
            state.Level, state.Tribe, state.TribeRole, state.GuildId, state.TeacherCharacterId,
            state.StudentCharacterId, !state.Friends.IsEmpty, neutralHomeZoneOnline));

        if (outcome != ForcedNeutralTribeResetOutcome.Success)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 forced-neutral tribe reset (8100) rejected: {Outcome} (tribe {Tribe}, level {Level})",
                context.CharacterId, outcome, state.Tribe, state.Level);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var oldTribe = state.Tribe;

        var remaining = CashItemStackConsumption.RemainingQuantity(context.Item.ItemId, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        await characters.ApplyTribeFourConversionAsync(context.CharacterId, ForcedNeutralTribeResetGate.NeutralTribe,
            state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort, state.QuestTargetPhase,
            state.QuestKillCounter, consumeSharedQuota: false, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Tribe: ForcedNeutralTribeResetGate.NeutralTribe),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 forced-neutral tribe reset mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 forced-neutral tribe reset (8100) applied: tribe {OldTribe} -> {NewTribe}",
            context.CharacterId, oldTribe, ForcedNeutralTribeResetGate.NeutralTribe);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private async ValueTask<bool> IsNeutralHomeZoneOnlineAsync(CancellationToken cancellationToken)
    {
        var mapId = options.ForcedNeutralResetHomeMapId;
        if (mapId <= 0)
            return false;

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        foreach (var shard in shards)
        {
            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, cancellationToken);
            if (hostedMaps.Contains(mapId))
                return true;
        }

        return false;
    }
}
