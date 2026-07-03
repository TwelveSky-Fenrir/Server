using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op19, CZ_PROCESS_DATA_SEND (contracts/03_inventory_craft.md, report 04_mega_switches.md §1) -- the
///     catch-all level-2 dispatch on <c>tSort</c>. This pass implements only the "most fundamental" container
///     moves the mission brief calls out: 208 (inventory&lt;-&gt;inventory), 210 (inventory-&gt;equipment), 213
///     (equipment-&gt;inventory) -- via <see cref="ContainerMatrix" />'s pure policy. Every OTHER tSort the
///     legacy switch itself recognizes (progression/skills 201-207 etc., container-move families this pass
///     doesn't cover yet -- Store/Trade/Bank/Hotkey-assign/1B-money/pet-bag/rune, GM 501-528, scripted duel
///     598-603, pet/maintenance 700-701) gets a clean <c>tResult</c> FAILURE reply instead of a disconnect --
///     see <see cref="ContainerMatrix.IsImplementedContainerMoveSort" />'s own remarks and this task's
///     openIssues for the full unimplemented list. A tSort absent from EVERY legacy family at all is the only
///     case that still gets <see cref="ClientSession.Abort" /> (anti-fuzzing, matching the legacy's own
///     <c>default:</c> branch).
/// </summary>
/// <remarks>
///     D7 regime (b): the affected container(s) are persisted SYNCHRONOUSLY -- via
///     <see cref="CharacterRepository.ReplaceContainerAsync" /> for a same-container move, or
///     <see cref="CharacterRepository.ReplaceTwoContainersAsync" /> (ONE transaction covering both) for a
///     cross-container move -- BEFORE the client ever sees a success reply: item state is a value object,
///     never write-behind (mission brief, "comme l'argent"), and a cross-container move must never be able to
///     durably remove an item from its source without also durably adding it to its destination. Once durable,
///     the ALREADY-COMPUTED result (containers + recomputed stats if Equipment was touched) is posted to
///     <see cref="Zone" /> via <see cref="Zone.PostInventoryCommand" /> so the zone's own tick -- the single
///     mutator of <see cref="PlayerRuntimeState" /> -- can mirror it; this handler never touches
///     <see cref="PlayerRuntimeState.Inventory" />/<see cref="PlayerRuntimeState.Stats" /> directly.
/// </remarks>
public sealed class GenericActionHandler(
    CharacterRepository characters,
    WorldDataCache worldData,
    ILogger<GenericActionHandler> logger)
    : IAsyncPacketHandler<GenericActionRequest>
{
    public async ValueTask HandleAsync(GenericActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        // Benign staleness (mid-handoff/disconnect) -- same defensive posture as every other InWorld handler.
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var sort = packet.Sort;

        if (!ContainerMatrix.IsKnownSort(sort))
        {
            // Anti-fuzzing default case (report 04 §PROCESS_DATA switch): a tSort that exists in NO legacy
            // family at all, not merely one Fenrir hasn't wired up yet.
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ContainerMatrix.IsImplementedContainerMoveSort(sort))
        {
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        // Structurally always 28 bytes available (Data is a fixed 130-byte array) -- defensive nonetheless,
        // mirroring the legacy's own "recast tData, bail on incoherence" contract.
        if (!DefaultPData.TryRead(packet.Data, out var move))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ContainerMatrix.TryResolveContainers(sort, move.Page1, move.Page2, out var fromContainer,
                out var toContainer))
        {
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        // Bounds-checked BEFORE any byte cast/dictionary lookup -- a client-controlled Index1/Index2 outside
        // [0, container max] must never be truncated into a byte that could accidentally alias a real slot.
        var sourceStack = ContainerMatrix.IsValidSlot(fromContainer, move.Index1)
            ? state.Inventory.GetSlot(fromContainer, (byte)move.Index1)
            : null;
        var destinationStack = ContainerMatrix.IsValidSlot(toContainer, move.Index2)
            ? state.Inventory.GetSlot(toContainer, (byte)move.Index2)
            : null;

        var sourceIsStackable = sourceStack is { } source &&
                                 worldData.ItemsById.TryGetValue(source.ItemId, out var sourceDefinition) &&
                                 ContainerMatrix.IsStackableSort(sourceDefinition.Item.Sort);

        var resolved = ContainerMatrix.ResolveMove(fromContainer, move.Index1, move.Quantity1, toContainer,
            move.Index2, sourceStack, destinationStack, sourceIsStackable);

        if (!resolved.Succeeded)
        {
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        if (resolved.Outcome == ContainerMatrix.MoveOutcome.NoOp)
        {
            // Same slot to itself -- nothing actually changed, no SQL, no zone command needed.
            SendResult(session, sort, packet.Data, success: true);
            return;
        }

        var projected = ContainerMatrix.ApplyMove(resolved, fromContainer, move.Index1,
            state.Inventory.GetContainer(fromContainer), toContainer, move.Index2,
            state.Inventory.GetContainer(toContainer));

        EffectiveStats? updatedStats = null;
        if (fromContainer == ContainerMatrix.Equipment || toContainer == ContainerMatrix.Equipment)
        {
            var equipmentContainer = fromContainer == ContainerMatrix.Equipment ? projected.From : projected.To;
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt,
                state.StatDex, state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);
            updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData);
        }

        // D7 regime (b): synchronous, awaited, BEFORE the client ever sees success -- see class remarks. A
        // cross-container move commits BOTH containers in ONE transaction (usp_CharacterItems_ReplaceTwoContainers):
        // two independent ReplaceContainerAsync calls here would let a fault between them durably remove an
        // item from its source without ever durably adding it to its destination -- a silent, permanent item
        // loss (review finding, Phase C/V2 integration). A same-container move (toContainer == fromContainer)
        // never had that risk -- projected.To already reflects fromContainer's own final state in that case.
        if (toContainer == fromContainer)
            await characters.ReplaceContainerAsync(characterId, fromContainer, ToTvps(projected.From),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, fromContainer, ToTvps(projected.From),
                toContainer, ToTvps(projected.To), cancellationToken);

        SendResult(session, sort, packet.Data, success: true);

        var containers = toContainer == fromContainer
            ? ImmutableArray.Create(new InventoryContainerSnapshot(fromContainer, projected.From))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(fromContainer, projected.From),
                new InventoryContainerSnapshot(toContainer, projected.To));

        if (!zone.PostInventoryCommand(new InventoryZoneCommand(characterId, containers, updatedStats)))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped container-move mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     ZC_PROCESS_DATA_RECV echoes the request's own <c>tData</c> blob back verbatim (report 04/contract
    ///     doc do not fully specify the response payload for this generic channel) -- a documented, reasonable
    ///     inference, not independently verified byte-for-bit (open issue).
    /// </summary>
    private static void SendResult(IPacketSession session, int sort, byte[] data, bool success)
    {
        session.Send(new GenericActionResponse { Result = success ? 0 : 1, Sort = sort, Data = data, RuneValue = 0 });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
