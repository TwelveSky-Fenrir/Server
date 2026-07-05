using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Runes;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_RUNE_SYSTEM_SEND (op157). Sort 0 = inventory-&gt;rune (insert), Sort 1 = rune-&gt;inventory (remove).
///     Any other Sort is silently ignored (no reply, no disconnect -- the legacy switch has no default case,
///     unlike almost every other opcode in this codebase). See <see cref="RuneSocketResolver" />'s remarks for
///     the client-supplied-item-id quirk on insert.
/// </summary>
/// <remarks>
///     Result correction vs. an earlier reading of this contract: 0 = INSERT ok, 1 = REMOVE ok, 2 = inventory
///     full (removal only) -- <c>B_RUNE_SYSTEM_RECV(0, ...)</c> terminates the Sort=0 branch and
///     <c>B_RUNE_SYSTEM_RECV(1, ...)</c> terminates the Sort=1 branch in S04_MyWork03.cpp:8162, the reverse of
///     <see cref="RuneSocketResponse" />'s original doc comment. Stat recompute (SetBasicAbilityFromEquip +
///     SetHPMP) is skipped: <c>aRuneSystemStat</c> has no consumer anywhere in Fenrir's stat pipeline yet, so
///     recomputing today would be a pure no-op (same posture as <c>DrinkBottleHandler</c>'s own remarks).
/// </remarks>
public sealed class RuneSocketHandler(
    ICharacterRepository characters,
    ILogger<RuneSocketHandler> logger)
    : IAsyncPacketHandler<RuneSocketRequest>
{
    public async ValueTask HandleAsync(RuneSocketRequest packet, IPacketSession session,
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
            switch (packet.Sort)
            {
                case 0:
                    await HandleInsertAsync(packet, session, zoneSession, zone, state, characterId,
                        cancellationToken);
                    return;
                case 1:
                    await HandleRemoveAsync(packet, session, zoneSession, zone, state, characterId,
                        cancellationToken);
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask HandleInsertAsync(RuneSocketRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (packet.Page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page, packet.Index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var source = state.Inventory.GetSlot((byte)packet.Page, (byte)packet.Index);
        if (source is not { } sourceStack)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = RuneSocketResolver.ResolveInsert(packet.RuneIndex, sourceStack.ItemId, state.RuneSystem);
        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var packedStat = ItemValueCodec.Encode(sourceStack.Enchant, sourceStack.Combine, sourceStack.Refine,
            sourceStack.Socket);
        var projectedContainer = state.Inventory.GetContainer((byte)packet.Page).Remove((byte)packet.Index);

        await characters.ReplaceContainerAsync(characterId, (byte)packet.Page, ToTvps(projectedContainer),
            cancellationToken);

        session.Send(new RuneSocketResponse
        {
            Result = 0, Page = packet.Page, Index = packet.Index, ItemIndex = packet.ItemIndex,
            RuneIndex = packet.RuneIndex
        });

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, packet.ItemIndex, packedStat, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} rune-socket inbox full: dropped insert mirror for character {CharacterId}",
                zone.MapId, characterId);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.Page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-insert inventory mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private async ValueTask HandleRemoveAsync(RuneSocketRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var destination = FindFreeSlot(state.Inventory);

        var resolved = RuneSocketResolver.ResolveRemove(packet.RuneIndex, state.RuneSystem, destination is not null);
        switch (resolved.Outcome)
        {
            case RuneSocketResolver.RemoveOutcome.Rejected:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case RuneSocketResolver.RemoveOutcome.InventoryFull:
                session.Send(new RuneSocketResponse { Result = 2, Page = 0, Index = 0, ItemIndex = 0, RuneIndex = 0 });
                return;
        }

        var (container, slot) = destination!.Value;
        var packedStat = state.RuneSystemStat[packet.RuneIndex];
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packedStat);
        var newStack = new ItemStack(resolved.ItemId, 0, enchant, combine, refine, socket, 0, 0, 0, 0, 0);
        var projectedContainer = state.Inventory.GetContainer(container).SetItem(slot, newStack);

        await characters.ReplaceContainerAsync(characterId, container, ToTvps(projectedContainer),
            cancellationToken);

        session.Send(new RuneSocketResponse
        {
            Result = 1, Page = container, Index = slot, ItemIndex = resolved.ItemId, RuneIndex = packet.RuneIndex
        });

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, null, null, null), cancellationToken))
            logger.LogError(
                "Zone {MapId} rune-socket inbox full: dropped remove mirror for character {CharacterId}",
                zone.MapId, characterId);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-remove inventory mirror for character {CharacterId}",
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
