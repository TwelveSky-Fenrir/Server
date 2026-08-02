using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingCatchService(ICharacterRepository characters, ILogger<FishingCatchService> logger)
    : IFishingCatchService
{
    public async ValueTask ResolveAndApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var castAt = DateTime.UtcNow;
        var step = state.FishingStep;

        if (step == 4)
        {
            var itemId = FishingRewardResolver.RollRewardItem(SystemRandomSource.Instance);
            var freeSlot = FindFreeSlot(state.Inventory);

            if (freeSlot is not { } destination)
            {
                logger.LogInformation(
                    "Fishing catch denied for character {CharacterId}: inventory full (item {ItemId})",
                    characterId, itemId);

                session.Send(new FishingCatchResponse
                    { Result = 2, ItemIndex = itemId, Page = -1, Index = -1, XY = -1 });

                if (!await zone.PostFishingCommandAndWaitAsync(
                        new FishingZoneCommand(characterId, 0, 0, state.CatchingFish, false, null, castAt),
                        cancellationToken))
                    logger.LogError(
                        "Zone {MapId} fishing inbox full: dropped catch-abort mirror for character {CharacterId}",
                        zone.MapId, characterId);
                return;
            }

            var newStack = new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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
                session.Send(new FishingCatchResponse
                    { Result = 2, ItemIndex = itemId, Page = -1, Index = -1, XY = -1 });

                if (!await zone.PostFishingCommandAndWaitAsync(
                        new FishingZoneCommand(characterId, 0, 0, state.CatchingFish, false, null, castAt),
                        cancellationToken))
                    logger.LogError(
                        "Zone {MapId} fishing inbox full: dropped catch-abort mirror for character {CharacterId}",
                        zone.MapId, characterId);
                return;
            }

            var rowPosition = destination.Slot / 8;

            session.Send(new FishingCatchResponse
            {
                Result = 1, ItemIndex = itemId, Page = destination.Container, Index = destination.Slot,
                XY = rowPosition
            });

            logger.LogInformation(
                "Character {CharacterId} caught fish reward item {ItemId} into container {Container} slot {Slot}",
                characterId, itemId, destination.Container, destination.Slot);

            var containers =
                ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
            if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} inventory inbox full: dropped fishing-catch mirror for character {CharacterId}",
                    zone.MapId, characterId);
        }

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = 1,
            FishingState = state.FishingState, FishingStep = step
        });

        int? actionSort = step switch { 4 => 94, 5 => 95, _ => null };
        if (!await zone.PostFishingCommandAndWaitAsync(
                new FishingZoneCommand(characterId, state.FishingState, step, state.CatchingFish, true, actionSort,
                    castAt), cancellationToken))
            logger.LogError("Zone {MapId} fishing inbox full: dropped catch mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static (byte Container, byte Slot)? FindFreeSlot(InventoryState inventory)
    {
        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage0, slot) is null)
                return (ContainerMatrix.InventoryPage0, slot);

        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage1, slot) is null)
                return (ContainerMatrix.InventoryPage1, slot);

        return null;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
