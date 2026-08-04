using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingCatchService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<FishingCatchService> logger)
    : IFishingCatchService
{
    public async ValueTask ResolveAndApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var castAt = DateTime.UtcNow;
        var step = state.FishingStep;

        if ((await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, state.FishingState, step, state.CatchingFish, false, null,
                    castAt, BiteWasHit: state.FishingBiteWasHit), cancellationToken)).Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogError("Zone {MapId} did not acknowledge fishing-catch state for character {CharacterId}",
                zone.MapId, characterId);
            session.Abort(DisconnectReason.Faulted);
            return;
        }

        if (step == 4)
        {
            var itemId = FishingRewardResolver.RollRewardItem(SystemRandomSource.Instance);
            if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            {
                logger.LogError("Fishing reward {ItemId} is not defined", itemId);

                if (!await ResetFishingAsync(zone, characterId, castAt, cancellationToken))
                    session.Abort(DisconnectReason.Faulted);

                return;
            }

            var freeSlot = InventoryFreeSlotFinder.Find(state.Inventory, worldData, itemId, state.InventoryDate,
                GameDate.Today());

            if (freeSlot is not { } destination)
            {
                logger.LogInformation(
                    "Fishing catch denied for character {CharacterId}: inventory full (item {ItemId})",
                    characterId, itemId);

                if (!await ResetFishingAsync(zone, characterId, castAt, cancellationToken))
                {
                    logger.LogError("Zone {MapId} did not acknowledge fishing-catch reset for character {CharacterId}",
                        zone.MapId, characterId);
                    session.Abort(DisconnectReason.Faulted);
                    return;
                }

                session.Send(new FishingCatchResponse
                    { Result = 2, ItemIndex = itemId, Page = -1, Index = -1, XY = -1 });
                return;
            }

            var rewardQuantity = FishingRewardResolver.NormalizeRewardQuantity(itemDefinition.Item.Sort);
            var newStack = new ItemStack(itemId, rewardQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0, destination.X,
                destination.Y);
            var projectedContainer =
                state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

            try
            {
                await characters.ReplaceContainerAsync(characterId, destination.Container,
                    ToTvps(projectedContainer), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} fishing-catch ReplaceContainerAsync failed (treated as inventory full)",
                    characterId);
                if (!await ResetFishingAsync(zone, characterId, castAt, cancellationToken))
                {
                    logger.LogError("Zone {MapId} did not acknowledge fishing-catch reset for character {CharacterId}",
                        zone.MapId, characterId);
                    session.Abort(DisconnectReason.Faulted);
                    return;
                }

                session.Send(new FishingCatchResponse
                    { Result = 2, ItemIndex = itemId, Page = -1, Index = -1, XY = -1 });
                return;
            }

            var containers =
                ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
            if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                    cancellationToken))
            {
                logger.LogError("Zone {MapId} did not acknowledge fishing-catch inventory for character {CharacterId}",
                    zone.MapId, characterId);
                session.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new FishingCatchResponse
            {
                Result = 1, ItemIndex = itemId, Page = destination.Container, Index = destination.Slot,
                XY = destination.GridIndex
            });
        }

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 1,
            FishingState = state.FishingState, FishingStep = step
        });

        int? actionSort = step switch { 4 => 94, 5 => 95, _ => null };

        if ((await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, 0, 0, false, true, actionSort, ApplyState: false),
                cancellationToken)).Kind != ZoneCommandResultKind.Applied)
            logger.LogError("Zone {MapId} did not acknowledge fishing-catch action for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private static async ValueTask<bool> ResetFishingAsync(Zone zone, int characterId, DateTime castAt,
        CancellationToken cancellationToken)
    {
        return (await zone.PostFishingCommandAndWaitForResultAsync(
            new FishingZoneCommand(characterId, 0, 0, false, false, null, castAt), cancellationToken)).Kind ==
               ZoneCommandResultKind.Applied;
    }
}
