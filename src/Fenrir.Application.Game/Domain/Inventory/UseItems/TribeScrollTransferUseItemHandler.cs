using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Domain.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class TribeScrollTransferUseItemHandler(
    ITribeConversionRepository tribeConversion,
    TribeConversionResolver resolver,
    PartyRegistry partyRegistry,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    WorldDataCache worldData,
    GameServerOptions options,
    ILogger<TribeScrollTransferUseItemHandler> logger) : IUseItemHandler
{
    public static readonly ImmutableArray<int> HandledItemIds = [8153, 8154];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        var toTribe = context.Value is >= 0 and <= 255 ? (byte)context.Value : (byte)255;

        var homeZoneOnline = await IsFactionTransferHomeZoneOnlineAsync(cancellationToken);
        var cape = state.Inventory.GetSlot(ContainerMatrix.Equipment, SkillGradeAuthority.CapeSlotIndex);

        var outcome = TribeScrollTransferGate.Evaluate(new TribeScrollTransferEligibilityContext(
            toTribe, state.Tribe, state.PreviousTribe, state.Level, state.TribeRole, state.GuildId,
            state.TeacherCharacterId, state.StudentCharacterId, !state.Friends.IsEmpty,
            partyRegistry.IsInParty(context.CharacterId), cape is not null, context.Zone.MapId, homeZoneOnline));

        if (outcome != TribeScrollTransferOutcome.Success)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 faction-transfer scroll ({ItemId}) rejected: {Outcome} (tribe {Tribe}->{ToTribe}, level {Level})",
                context.CharacterId, context.Item.ItemId, outcome, state.Tribe, toTribe, state.Level);

            switch (outcome)
            {
                case TribeScrollTransferOutcome.InvalidDestinationTribe:
                {
                    if (state.Session is ClientSession client)
                        client.Abort(DisconnectReason.Malformed);
                    break;
                }
                case TribeScrollTransferOutcome.AlreadyTargetTribe:
                {
                    if (state.Session is ClientSession client)
                        client.Abort(DisconnectReason.StateViolation);
                    break;
                }
            }

            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var fromTribe = state.PreviousTribe;

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var newEquipmentBuilder = equipmentContainer.ToBuilder();
        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot == PetSlots.EquipmentSlot)
                continue;

            if (resolver.TryRemapItem(fromTribe, toTribe, stack.ItemId, out var newItemId) &&
                newItemId != stack.ItemId)
                newEquipmentBuilder[slot] = stack with { ItemId = newItemId };
        }

        var newEquipment = newEquipmentBuilder.ToImmutable();

        var skillChanges = new List<(byte Slot, LearnedSkill NewSkill)>();
        foreach (var (slot, skill) in state.LearnedSkills)
            if (resolver.TryRemapSkill(fromTribe, toTribe, skill.SkillId, out var newSkillId) &&
                newSkillId != skill.SkillId)
                skillChanges.Add((slot, skill with { SkillId = newSkillId }));

        var projectedPage = state.Inventory.GetContainer(context.Page).Remove(context.Index);
        var items = ToTvps(projectedPage);

        await tribeConversion.ApplyTribeScrollConversionAsync(context.CharacterId, context.Item.ItemId, toTribe,
            context.Page, items, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Tribe: toTribe, PreviousTribe: toTribe),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 faction-transfer tribe mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        var updatedStats = UseItemStatRecompute.WithEquipment(state, worldData, newEquipment);

        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(context.Page, projectedPage),
            new InventoryContainerSnapshot(ContainerMatrix.Equipment, newEquipment));

        if (!await context.Zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(context.CharacterId, containers, updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 faction-transfer inventory mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        foreach (var (slot, newSkill) in skillChanges)
            if (!context.Zone.PostSkillCommand(new SkillZoneCommand(context.CharacterId, slot, newSkill,
                    state.SkillPoints)))
                logger.LogError(
                    "Zone {MapId} skill inbox full: dropped op23 faction-transfer skill-remap mirror (slot {Slot}) for character {CharacterId}",
                    context.Zone.MapId, context.CharacterId, slot);

        logger.LogInformation(
            "Character {CharacterId} op23 faction-transfer scroll ({ItemId}) applied: tribe {FromTribe}->{ToTribe}",
            context.CharacterId, context.Item.ItemId, fromTribe, toTribe);

        state.Session.Send(new ReturnToHomeZoneResponse());

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private async ValueTask<bool> IsFactionTransferHomeZoneOnlineAsync(CancellationToken cancellationToken)
    {
        var mapId = options.FactionTransferHomeZoneMapId;
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
