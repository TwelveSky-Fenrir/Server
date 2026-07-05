using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Fishing;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_FISHING_REWARD_SEND (opcode 105) -- same zone-52 gating as <see cref="FishingLineHandler" />.
///     Silently ignored unless currently in the "catch" state (step 4/5, <c>CatchingFish</c>). Step 4 rolls a
///     koi item and grants it (Result=2/inventory-full resets fishing to idle with no further reply, matching
///     the legacy's early return); step 5 is a pure miss -- no item, straight to the echoed progress reply.
///     Legacy quirk (kept, not "fixed" per D8): a successful/miss catch does NOT reset FishingState/FishingStep,
///     so a repeated CZ_FISHING_REWARD_SEND while still in step 4 re-rolls and re-grants another item.
/// </summary>
public sealed class FishingCatchHandler(ICharacterRepository characters, ILogger<FishingCatchHandler> logger)
    : IAsyncPacketHandler<FishingCatchRequest>
{
    public async ValueTask HandleAsync(FishingCatchRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (state.FishingState == 0 || !state.CatchingFish)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(session, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(IPacketSession session, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        var castAt = DateTime.UtcNow;
        var step = state.FishingStep;

        if (step == 4)
        {
            var itemId = FishingRewardResolver.RollRewardItem(SystemRandomSource.Instance);
            var freeSlot = FindFreeSlot(state.Inventory);

            if (freeSlot is not { } destination)
            {
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

            session.Send(new FishingCatchResponse
            {
                Result = 1, ItemIndex = itemId, Page = destination.Container, Index = destination.Slot, XY = 0
            });

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
