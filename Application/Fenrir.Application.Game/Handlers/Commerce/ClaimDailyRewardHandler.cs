using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_CLAIM_REWARD_ITEM_SEND (opcode 155, contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:15648-15699</c>).
///     "Already claimed today" is modeled as a date comparison (<see cref="GameDate.Today" /> vs.
///     game.Characters.RewardClaimDate) rather than the legacy's own per-session <c>uRewardClaimState</c>
///     flag -- no daily-reset call site for that flag was locatable in the available source. Granted
///     quantity is always 1 (the legacy's own quantity param is really just a Sort==99 coupon display
///     flag, not a stack size). Claim-day advance and item grant commit atomically
///     (<see cref="CharacterRepository.ClaimDailyRewardAsync" />).
/// </summary>
/// <remarks>
///     OPEN ISSUE: the wire's <c>InvenX</c>/<c>InvenY</c> sub-cell position has no Fenrir equivalent (same
///     gap as <see cref="World.Loot.GroundItemPickupPolicy" />) -- reported as (0, 0) on a successful claim.
/// </remarks>
public sealed class ClaimDailyRewardHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<ClaimDailyRewardHandler> logger)
    : IAsyncPacketHandler<ClaimDailyRewardRequest>
{
    private const int RewardBundleId = 1;

    public async ValueTask HandleAsync(ClaimDailyRewardRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(ClaimDailyRewardRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var today = GameDate.Today();
        var claimState = await characters.GetRewardClaimStateAsync(characterId, today, cancellationToken);
        if (claimState is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (claimState.RewardClaimDate == today || claimState.RewardClaimDay > 6)
        {
            session.Send(new ClaimDailyRewardResponse
                { Result = 1, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 });
            return;
        }

        if (!worldData.RewardBundleItemsByBundleId.TryGetValue(RewardBundleId, out var slots))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var day = claimState.RewardClaimDay;
        var itemId = 0;
        foreach (var slot in slots)
            if (slot.SlotIndex == day + 1)
            {
                itemId = slot.ItemId ?? 0;
                break;
            }

        if (itemId < 1 || !worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
        {
            session.Send(new ClaimDailyRewardResponse
                { Result = 2, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 });
            return;
        }

        var newStack = new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer = state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        try
        {
            await characters.ClaimDailyRewardAsync(characterId, today, destination.Container,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} daily-reward claim ClaimDailyRewardAsync failed (treated as already claimed)",
                characterId);
            session.Send(new ClaimDailyRewardResponse
                { Result = 1, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 });
            return;
        }

        var coupon = itemDefinition.Item.Sort == 99 ? 1 : 0;
        session.Send(new ClaimDailyRewardResponse
        {
            Result = 0,
            Value = [itemId, 0, 0, coupon, 0, 0],
            InvenPage = destination.Container,
            InvenX = 0,
            InvenY = 0
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped daily-reward mirror for character {CharacterId}",
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
