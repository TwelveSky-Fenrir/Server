using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
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
                return;
            }

            // Legacy quirk: the response's 4th field (FishingCatchResponse.XY) carries only the row-equivalent
            // inventory-position value the item-add routine computes, discarding the column value it also
            // computes -- confirmed at the call sites (Server/ts25zone/S04_MyWork02.cpp:13895, 13935, 13942,
            // which pass the row out-param and discard the column one) and inside the routine itself
            // (MyUtil::SendItemToInventory, Server/ts25zone/S07_MyGame03.cpp:4744-4782). Legacy actually derives
            // that row from a second, distinct "visual grid position" index that a fragmentation-aware search
            // (MyUtil::FindEmptyInvenForItem, Server/ts25zone/S07_MyGame03.cpp:4557-4612) computes separately
            // from the raw storage slot; Fenrir's InventoryState has no equivalent split-index concept (a slot
            // here already IS the grid position), so this reproduces the row the same way it's already
            // decomposed elsewhere in this codebase for an 8-wide inventory grid (SkyUpgradeItemService,
            // UpgradeCapeService: slot % 8 = column, slot / 8 = row). This matches legacy exactly whenever the
            // visual grid position and the storage slot coincide (the common case); a follow-up
            // legacy-behavior-translator contract is needed to fully resolve the fragmented-inventory edge case
            // where they diverge.
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
