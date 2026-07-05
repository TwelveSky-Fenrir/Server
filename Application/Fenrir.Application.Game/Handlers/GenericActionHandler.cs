using Fenrir.Application.Game.Handlers.GenericAction.Services;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op19, CZ_PROCESS_DATA_SEND -- catch-all dispatch on tSort: container moves (inventory&lt;-&gt;inventory,
///     inventory&lt;-&gt;equipment), ground pickup, NPC teleport toll, skill learn/upgrade, and NPC shop buy/sell.
///     A tSort the legacy switch recognizes but this handler doesn't implement replies with a clean failure; a
///     tSort absent from every legacy family gets <see cref="ClientSession.Abort" /> (anti-fuzzing).
/// </summary>
/// <remarks>
///     Affected containers are persisted synchronously, before the client ever sees success. A cross-container
///     move commits both containers in one transaction so a mid-sequence fault can never durably remove an item
///     from its source without also adding it to its destination. Once durable, the result is posted to
///     <see cref="Zone" /> so the zone's own tick -- the sole mutator of <see cref="PlayerRuntimeState" /> --
///     can mirror it.
/// </remarks>
public sealed class GenericActionHandler(IGenericActionService genericActionService)
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
}
