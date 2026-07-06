using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

/// <inheritdoc cref="IInventoryToWorldDropService" />
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:1129-1233 (<c>ProcessForInventoryToWorld</c>) --
///     orchestration only; see <see cref="InventoryToWorldDropPolicy" /> for the actual ported rules and its
///     own remarks for the two deliberately-unresolved handoffs (the <c>CheckAvatarDrop</c> encoding, and the
///     shared spawn routine's item-type/packed-value gate) this service's own call sites below reproduce.
///     <para>
///         Does NOT call any <see cref="Zone" /> ground-item-spawn method -- <see cref="Zone.SpawnGroundItem" />
///         is documented tick-thread-only and, even if it weren't, has no parameter for Value/SerialNumber/
///         gem-socket data, so calling it here would either require an unsafe cross-thread call or silently
///         drop this operation's own unique-item socket carry-over requirement. The returned
///         <see cref="GroundItemSpawnPlan" /> is the hand-off point for whichever follow-up gives
///         <see cref="Zone" /> a capacity/value/socket-aware spawn entry point safely callable from here.
///     </para>
/// </remarks>
public sealed class InventoryToWorldDropService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    PartyRegistry partyRegistry,
    ILogger<InventoryToWorldDropService> logger)
    : IInventoryToWorldDropService
{
    /// <summary>
    ///     game.EventLog.EventCode for a manual unique-item ground drop -- an app-owned numbering scheme (see
    ///     <c>DestroyItemService.DestroyItemEventCode</c>'s own identical remark); picked as an arbitrary small
    ///     value scoped to this one path.
    /// </summary>
    private const short ManualDropItemEventCode = 1;

    private const byte SuccessOutcome = 1;

    /// <summary>
    ///     <c>ItemRowDto.CheckAvatarDrop</c>'s "disallow player drop" encoding -- see
    ///     <see cref="InventoryToWorldDropPolicy" />'s own remarks (handoff 1) for why this specific value is a
    ///     documented, NOT independently re-confirmed, analogy to the sibling <c>CheckAvatarTrade == 1</c>
    ///     convention rather than a re-derived citation of its own.
    /// </summary>
    private const byte NonDroppableFlagValue = 1;

    public async ValueTask<InventoryToWorldDropResult> DropToWorldAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, DefaultPData move, bool premiumPageAccessAllowed,
        CancellationToken cancellationToken)
    {
        var sourcePage = move.Page1;
        var sourceSlot = move.Index1;

        var source = sourcePage is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
                     ContainerMatrix.IsValidSlot((byte)sourcePage, sourceSlot)
            ? state.Inventory.GetSlot((byte)sourcePage, (byte)sourceSlot)
            : null;

        ItemDefinition? itemDefinition = source is { } stack && worldData.ItemsById.TryGetValue(stack.ItemId, out var definition)
            ? definition
            : null;

        var partyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, characterId, state.Name,
            memberId => zone.TryGetPlayer(memberId, out var member) ? member?.Name : null);

        var resolved = InventoryToWorldDropPolicy.Resolve(
            sourcePage, sourceSlot, move.Quantity1, premiumPageAccessAllowed,
            source, itemDefinition,
            sourceIsDroppableByPlayer: itemDefinition is null || itemDefinition.Item.CheckAvatarDrop != NonDroppableFlagValue,
            evaluateSpawnEligibility: static _ => GroundItemSpawnEligibility.Eligible,
            currentGroundItemCount: zone.GroundItemCount,
            dropperPosX: state.PosX, dropperPosY: state.PosY, dropperPosZ: state.PosZ,
            dropperName: state.Name, dropperPartyName: partyName);

        if (resolved.IsMalformed)
            return InventoryToWorldDropResult.Aborted;

        if (resolved.IsSoftFailure)
            return InventoryToWorldDropResult.Failed;

        var container = (byte)sourcePage;
        var slot = (byte)sourceSlot;
        var projected = resolved.NewSource is { } newStack
            ? state.Inventory.GetContainer(container).SetItem(slot, newStack)
            : state.Inventory.GetContainer(container).Remove(slot);

        await characters.ReplaceContainerAsync(characterId, container, ToTvps(projected), cancellationToken);

        // Unique-item-only audit row -- the stackable path records no equivalent entry (see this operation's
        // own behavior contract's Edge cases, and InventoryToWorldDropPolicy's class remarks).
        if (source is { } droppedStack && !ContainerMatrix.IsStackableSort(itemDefinition!.Item.Sort))
            await eventLog.LogAsync(ManualDropItemEventCode, EventLogCategory.ItemDrop, accountId, characterId,
                null, null, null, null, null, droppedStack.ItemId, droppedStack.Quantity, SuccessOutcome,
                $"Serial={droppedStack.Serial};Enchant={droppedStack.Enchant};Combine={droppedStack.Combine};" +
                $"Refine={droppedStack.Refine};Socket={droppedStack.Socket}",
                cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped inventory-to-world mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new InventoryToWorldDropResult(InventoryToWorldDropStatus.Succeeded, resolved.Spawn);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
