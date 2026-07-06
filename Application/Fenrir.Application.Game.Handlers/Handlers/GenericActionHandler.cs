using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op19, CZ_PROCESS_DATA_SEND -- catch-all dispatch on tSort: container moves (inventory&lt;-&gt;inventory,
///     inventory&lt;-&gt;equipment), ground pickup, manual drop-to-world, NPC teleport toll, skill learn/upgrade,
///     NPC shop buy/sell, and rune-stone stat crafting. A tSort the legacy switch recognizes but this handler
///     doesn't implement replies with a clean failure; a tSort absent from every legacy family gets
///     <see cref="ClientSession.Abort" /> (anti-fuzzing).
/// </summary>
/// <remarks>
///     Affected containers are persisted synchronously, before the client ever sees success. A cross-container
///     move commits both containers in one transaction so a mid-sequence fault can never durably remove an item
///     from its source without also adding it to its destination. Once durable, the result is posted to
///     <see cref="Zone" /> so the zone's own tick -- the sole mutator of <see cref="PlayerRuntimeState" /> --
///     can mirror it.
/// </remarks>
public sealed class GenericActionHandler(
    IGenericActionService genericActionService,
    IInventoryToWorldDropService inventoryToWorldDropService,
    IRuneStoneCraftService runeStoneCraftService)
    : IAsyncPacketHandler<GenericActionRequest>
{
    public async ValueTask HandleAsync(GenericActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes this whole dispatch per character -- SkillPoints/Inventory/money are shared racy state
        // across every sort this handler covers, not just the money-bearing ones.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var sort = packet.Sort;

        if (!ContainerMatrix.IsKnownSort(sort))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tSort 201 (ground pickup) doesn't fit the container-move (from,to) shape below -- its "from" is the
        // zone's ground-item pool, not a player container.
        if (sort == 201)
        {
            var result = await genericActionService.PickupGroundItemAsync(packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            if (result.NotifyQuestProgress)
                session.Send(new QuestProgressResponse { Sort = 7, Page = 0, Index = 0, XPost = 0, YPost = 0 });
            return;
        }

        if (sort == 207)
        {
            var result = await genericActionService.PayTeleportTollAsync(packet.Data, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 209 -- ProcessForInventoryToWorld (drop item from inventory to the ground). Shares the same
        // generic DefaultPData envelope as the container-move family below (ServerDocs/12_ts25zone/
        // 03_MyWork_Dispatch_Framing.md §8.1: case 209 casts tData as DEFAULT_PDATA_RECV, unmodified field
        // order), but its destination is the zone's ground-item pool rather than a ContainerMatrix container,
        // so it can't go through MoveContainerAsync.
        if (sort == 209)
        {
            if (!DefaultPData.TryRead(packet.Data, out var dropMove))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            // No wInventoryDate-equivalent field exists on PlayerRuntimeState yet -- same pre-existing gap the
            // already-shipped 208/210/213 container moves below also leave unaddressed (MoveContainerAsync
            // never checks a premium-page expiry either), so this stays a hardcoded pass until that lands.
            // AccountId is guaranteed set alongside CharacterId (both written together by MarkTicketConsumed
            // before InWorld is reachable) -- same non-null posture as characterId above.
            var dropResult = await inventoryToWorldDropService.DropToWorldAsync(zone, state, characterId,
                zoneSession.AccountId!.Value, dropMove, premiumPageAccessAllowed: true, cancellationToken);
            RespondDrop(session, zoneSession, sort, packet.Data, dropResult);
            return;
        }

        if (sort is 202 or 233)
        {
            var result = await genericActionService.LearnSkillAsync(sort, packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        if (sort == 203)
        {
            var result = await genericActionService.UpgradeSkillAsync(packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 212/252/215: NPC-shop sell/buy. Neither side is a ContainerMatrix container (sell's destination
        // is the NPC's own catalog; buy's Page1/Index1 repurpose the wire shape as NpcId/ItemId), so these are
        // handled as their own branch rather than folded into ContainerMatrix.TryResolveContainers.
        if (sort is 212 or 252 or 215)
        {
            if (!DefaultPData.TryRead(packet.Data, out var shopMove))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            var result = sort == 215
                ? await genericActionService.BuyFromNpcShopAsync(zone, state, characterId, shopMove,
                    cancellationToken)
                : await genericActionService.SellToNpcShopAsync(zone, state, characterId, shopMove,
                    cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 3000 -- ProcessForInventoryToInventoryRune (rune-stone stat crafting). Same generic
        // DefaultPData envelope, tPage1/tIndex1/tPage2/tIndex2 unmodified and tXPost2 repurposed as the
        // 100/200/300/400 stat-slot selector (ServerDocs/12_ts25zone/07_MyWork05_Helpers.md §6.1/§8.1;
        // IRuneStoneCraftService.CraftAsync's own param docs cite the same mapping).
        if (sort == 3000)
        {
            if (!DefaultPData.TryRead(packet.Data, out var runeMove))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            // destinationPackedStat: no dedicated column exists yet for a rune-core item's packed
            // STR/DEX/VIT/INT value (RuneStoneCraftService's own remarks) -- passed as a fixed 0 (the "no
            // stat slot filled yet" reading) until fenrir-database-engineer adds one or cpp-zone-gameplay-analyst
            // confirms an existing column is reused. secondInventoryPageAccessible has the same pre-existing
            // gap as the 209 branch above.
            var runeResult = await runeStoneCraftService.CraftAsync(runeMove.Page1, runeMove.Index1,
                runeMove.Page2, runeMove.Index2, runeMove.XPost2, destinationPackedStat: 0,
                secondInventoryPageAccessible: true, zone, state, characterId, cancellationToken);
            RespondRune(session, zoneSession, sort, packet.Data, runeResult);
            return;
        }

        var moveResult = await genericActionService.MoveContainerAsync(sort, packet.Data, zone, state, characterId,
            cancellationToken);
        Respond(session, zoneSession, sort, packet.Data, moveResult);
    }

    private static void Respond(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        GenericActionResult result)
    {
        if (result.Status == GenericActionStatus.Aborted)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GenericActionResponse
        {
            Result = result.Status == GenericActionStatus.Succeeded ? 0 : 1, Sort = sort, Data = data, RuneValue = 0
        });
    }

    private static void RespondDrop(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        InventoryToWorldDropResult result)
    {
        if (result.Status == InventoryToWorldDropStatus.Aborted)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GenericActionResponse
        {
            Result = result.Status == InventoryToWorldDropStatus.Succeeded ? 0 : 1, Sort = sort, Data = data,
            RuneValue = 0
        });
    }

    private static void RespondRune(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        RuneStoneCraftResult result)
    {
        if (result.Outcome == RuneStoneCraftOutcome.Disconnect)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GenericActionResponse
        {
            Result = result.ResultCode, Sort = sort, Data = data, RuneValue = result.NewPackedStat
        });
    }
}
