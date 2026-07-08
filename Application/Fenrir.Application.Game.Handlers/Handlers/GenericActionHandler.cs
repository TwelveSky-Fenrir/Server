using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op19, CZ_PROCESS_DATA_SEND -- catch-all dispatch on tSort: container moves (inventory&lt;-&gt;inventory,
///     inventory&lt;-&gt;equipment), ground pickup, manual drop-to-world, NPC teleport toll, skill learn/upgrade,
///     stat-point allocation, NPC shop buy/sell, rune-stone stat crafting, TimeExchange
///     (play-time-event-to-teacher-point/pet-experience conversion), the Store/coffre (menu index 2) and
///     Save/vault (menu index 8, account-scoped bank) item/money transfer families, and GM-BLOCK (tSort 519 --
///     legacy has no dedicated wire opcode for this command; it rides this same envelope like everything else
///     here). A tSort the legacy switch recognizes but this handler doesn't implement
///     replies with a clean failure; a tSort absent from every legacy family gets
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
    IRuneStoneCraftService runeStoneCraftService,
    IGmBlockAvatarService gmBlockAvatarService,
    ILogger<GenericActionHandler> logger)
    : IAsyncPacketHandler<GenericActionRequest>
{
    /// <summary>AvatarStatUpdateResponse.Sort for S014PET_EXP, self-addressed only (S05_MyTransfer.cpp:519-542).</summary>
    private const int PetExperienceStatSort = 14;

    public async ValueTask HandleAsync(GenericActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: GenericActionRequest received, Sort {Sort}",
                zoneSession.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: GenericActionRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

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

        // Gated on IsEnabled rather than relying on the ad-hoc LogDebug call's own internal check: this
        // dispatch runs once per CZ_PROCESS_DATA_SEND packet across the whole player base (inventory moves,
        // NPC shop trades, stat points, Store/Save transfers), so the per-branch "which Sort went where" line
        // below is on a comparably hot path to SessionLoop.ProcessBufferAsync's own PacketReceived logging --
        // same debugEnabled-local shape as that method, see this handler's own class remarks.
        var debugEnabled = logger.IsEnabled(LogLevel.Debug);

        if (!ContainerMatrix.IsKnownSort(sort))
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} unrecognized, aborting",
                    zoneSession.SessionId, characterId, sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tSort 201 (ground pickup) doesn't fit the container-move (from,to) shape below -- its "from" is the
        // zone's ground-item pool, not a player container.
        if (sort == 201)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.PickupGroundItemAsync));
            var result = await genericActionService.PickupGroundItemAsync(packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            if (result.NotifyQuestProgress)
                session.Send(new QuestProgressResponse { Sort = 7, Page = 0, Index = 0, XPost = 0, YPost = 0 });
            return;
        }

        if (sort == 207)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.PayTeleportTollAsync));
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
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed DefaultPData payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IInventoryToWorldDropService.DropToWorldAsync));

            // No wInventoryDate-equivalent field exists on PlayerRuntimeState yet -- same pre-existing gap the
            // already-shipped 208/210/213 container moves below also leave unaddressed (MoveContainerAsync
            // never checks a premium-page expiry either), so this stays a hardcoded pass until that lands.
            // AccountId is guaranteed set alongside CharacterId (both written together by MarkTicketConsumed
            // before InWorld is reachable) -- same non-null posture as characterId above.
            var dropResult = await inventoryToWorldDropService.DropToWorldAsync(zone, state, characterId,
                zoneSession.AccountId!.Value, dropMove, true, cancellationToken);
            RespondDrop(session, zoneSession, sort, packet.Data, dropResult);
            return;
        }

        if (sort is 202 or 233)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.LearnSkillAsync));
            var result = await genericActionService.LearnSkillAsync(sort, packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        if (sort == 203)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.UpgradeSkillAsync));
            var result = await genericActionService.UpgradeSkillAsync(packet.Data, zone, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 206 -- ProcessForStatPlus (spend aStatPoint into raw Str/Dex/Vit/Int). STAT_PLUS_RECV
        // (Server/Header/Protocol/STRUCT.h:1261-1265) is a bare two-int struct -- tStatSort then tAddValue,
        // back-to-back, no leading fields -- that doesn't share DefaultPData's 7-field/28-byte shape, so it's
        // read directly off packet.Data rather than force-fit through DefaultPData.TryRead. Mirrors
        // Server/ts25zone/S04_MyWork04.cpp:420-428, which casts tData directly to STAT_PLUS_RECV* at offset 0.
        // AllocateStatPointAsync already treats every rejection (illegal category, unaffordable amount) as
        // GenericActionResult.Aborted, and Respond() below already disconnects on Aborted -- matching the
        // legacy dispatcher's own "no acknowledgment on failure" behavior (S04_MyWork04.cpp:303-306,354)
        // with no extra handling needed here.
        if (sort == 206)
        {
            var statSort = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(0, 4));
            var addValue = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(4, 4));
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method} (statSort {StatSort}, addValue {AddValue})",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.AllocateStatPointAsync),
                    statSort, addValue);
            var result = await genericActionService.AllocateStatPointAsync(statSort, addValue, zone, state,
                characterId, cancellationToken);
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
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed DefaultPData payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    sort == 215
                        ? nameof(IGenericActionService.BuyFromNpcShopAsync)
                        : nameof(IGenericActionService.SellToNpcShopAsync));

            // AccountId is guaranteed set alongside CharacterId (both written together by MarkTicketConsumed
            // before InWorld is reachable) -- same non-null posture as the 209 branch above.
            var result = sort == 215
                ? await genericActionService.BuyFromNpcShopAsync(zone, state, zoneSession.AccountId!.Value,
                    characterId, shopMove, cancellationToken)
                : await genericActionService.SellToNpcShopAsync(zone, state, zoneSession.AccountId!.Value,
                    characterId, shopMove, cancellationToken);
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
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed DefaultPData payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IRuneStoneCraftService.CraftAsync));

            // destinationPackedStat: no dedicated column exists yet for a rune-core item's packed
            // STR/DEX/VIT/INT value (RuneStoneCraftService's own remarks) -- passed as a fixed 0 (the "no
            // stat slot filled yet" reading) until fenrir-database-engineer adds one or cpp-zone-gameplay-analyst
            // confirms an existing column is reused. secondInventoryPageAccessible has the same pre-existing
            // gap as the 209 branch above.
            var runeResult = await runeStoneCraftService.CraftAsync(runeMove.Page1, runeMove.Index1,
                runeMove.Page2, runeMove.Index2, runeMove.XPost2, 0,
                true, zone, state, characterId, cancellationToken);
            RespondRune(session, zoneSession, sort, packet.Data, runeResult);
            return;
        }

        // tSort 237 -- TimeExchange (converts accrued play-time-event minutes into teacher points + pet
        // experience). Unlike the neighboring sort case immediately before it in the legacy dispatch
        // (S04_MyWork04.cpp:895-921), this one has no NPC-proximity/cooldown precondition at all
        // (S04_MyWork04.cpp:916-920). Sorts 235/236 sit adjacent in the same dispatch family but are
        // distinct, unrelated actions -- not handled here.
        if (sort == 237)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TimeExchangeAsync));
            var timeExchangeResult = await genericActionService.TimeExchangeAsync(zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, timeExchangeResult);
            if (timeExchangeResult.GrantedPetExperienceGrowth is { } newPetGrowth)
                session.Send(new AvatarStatUpdateResponse
                    { Sort = PetExperienceStatSort, Value = newPetGrowth, Value2 = 0 });
            return;
        }

        // tSort 223/250/224/248/225 -- Store/coffre item deposit/withdraw/rearrange (menu index 2). tPage1/
        // tPage2 aren't always ContainerMatrix-only ids the way 208/210/213 are (Store's own StorePage0/1),
        // so this is its own branch rather than folded into MoveContainerAsync's ImplementedContainerMoveSorts
        // set -- see GenericActionService.TransferStoreItemAsync's own remarks for why. No NPC-proximity gate
        // (the "Remote Storage Fix" patch already disabled it in the reference source).
        if (sort is 223 or 250 or 224 or 248 or 225)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferStoreItemAsync));
            var result = await genericActionService.TransferStoreItemAsync(sort, packet.Data, zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 226/227 -- Store/coffre money deposit/withdraw (menu index 2).
        if (sort is 226 or 227)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferStoreMoneyAsync));
            var result = await genericActionService.TransferStoreMoneyAsync(sort, packet.Data, zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 228/251/229/249/230 -- Save/vault (account-scoped bank) item deposit/withdraw/rearrange (menu
        // index 8). Crosses into game.AccountVault/AccountVaultItems (account-scoped, not character-scoped),
        // so this can never be a ContainerMatrix container move either. No NPC-proximity gate (the "Remote
        // Save Storage Fix" patch already disabled it in the reference source).
        if (sort is 228 or 251 or 229 or 249 or 230)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferBankItemAsync));
            var result = await genericActionService.TransferBankItemAsync(sort, packet.Data, zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 231/232 -- Save/vault (account bank) money deposit/withdraw (menu index 8).
        if (sort is 231 or 232)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferBankMoneyAsync));
            var result = await genericActionService.TransferBankMoneyAsync(sort, packet.Data,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        // tSort 519 -- [GM]-BLOCK (Server/ts25zone/S04_MyWork04.cpp:1487-1515). Legacy has no dedicated wire
        // opcode for this command; it is multiplexed inside this same generic envelope like every other tSort
        // in this handler. IGmBlockAvatarService owns every send/abort on this path itself -- its own three
        // outcomes are asymmetric (unauthorized caller disconnects with no reply, target-not-found sends the
        // shared opcode-23 GenericActionResponse ack, success sends the caller nothing at all) -- so this
        // branch never calls Respond() itself, unlike every sort above it.
        if (sort == 519)
        {
            if (!GmBlockAvatarPayload.TryRead(packet.Data, out var gmBlockPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmBlockAvatarPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBlockAvatarService.HandleAsync));
            await gmBlockAvatarService.HandleAsync(gmBlockPayload, zoneSession, cancellationToken);
            return;
        }

        if (debugEnabled)
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.MoveContainerAsync));
        var moveResult = await genericActionService.MoveContainerAsync(sort, packet.Data, zone, state, characterId,
            cancellationToken);
        Respond(session, zoneSession, sort, packet.Data, moveResult);
    }

    private void Respond(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        GenericActionResult result)
    {
        if (result.Status == GenericActionStatus.Aborted)
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted (anti-fuzzing/malformed-input gate)",
                zoneSession.SessionId, zoneSession.CharacterId, sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (result.Status != GenericActionStatus.Succeeded)
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} rejected (clean failure)",
                zoneSession.SessionId, zoneSession.CharacterId, sort);

        session.Send(new GenericActionResponse
        {
            Result = result.Status == GenericActionStatus.Succeeded ? 0 : 1, Sort = sort, Data = data, RuneValue = 0
        });
    }

    private void RespondDrop(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        InventoryToWorldDropResult result)
    {
        if (result.Status == InventoryToWorldDropStatus.Aborted)
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} (inventory-to-world drop) aborted (anti-fuzzing/malformed-input gate)",
                zoneSession.SessionId, zoneSession.CharacterId, sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (result.Status != InventoryToWorldDropStatus.Succeeded)
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} (inventory-to-world drop) rejected (clean failure)",
                zoneSession.SessionId, zoneSession.CharacterId, sort);

        session.Send(new GenericActionResponse
        {
            Result = result.Status == InventoryToWorldDropStatus.Succeeded ? 0 : 1, Sort = sort, Data = data,
            RuneValue = 0
        });
    }

    private void RespondRune(IPacketSession session, ZoneClientSession zoneSession, int sort, byte[] data,
        RuneStoneCraftResult result)
    {
        if (result.Outcome == RuneStoneCraftOutcome.Disconnect)
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} (rune-stone craft) aborted (anti-fuzzing/malformed-input gate)",
                zoneSession.SessionId, zoneSession.CharacterId, sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GenericActionResponse
        {
            Result = result.ResultCode, Sort = sort, Data = data, RuneValue = result.NewPackedStat
        });
    }
}
