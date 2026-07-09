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
///     Save/vault (menu index 8, account-scoped bank) item/money transfer families, GM-BLOCK (tSort 519), the
///     Admin-tier spawn-item/MAX-stat-cheat/grant-pet-experience commands (tSort 505/523, 509, 700), the
///     Elevated-tier grant-experience-to-self/grant-money/zone-wide-FFA-start/summon-monster commands (tSort
///     503, 504, 333, 506), and the sixteen Basic-tier commands (HIDE/SHOW 501/502, self-teleport-to-coordinate
///     MOVE 507, DIE 508, TRIBE 510, EQUIP/UNEQUIP 511/512, FIND 513, CALL 514, self-teleport-to-target MOVE
///     515, NCHAT/YCHAT 516/517, KICK 518, TRIBEBANK 520, LEVEL 521, STR/DEX/VIT/INT-edit 522) -- none of these
///     GM commands have a dedicated legacy wire opcode; they all ride this same envelope like everything else
///     here. A tSort the legacy switch recognizes but this handler doesn't implement
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
    IGmCreateItemService gmCreateItemService,
    IGmMaxStatService gmMaxStatService,
    IGmPetExperienceGrantService gmPetExperienceGrantService,
    IGmExpGrantService gmExpGrantService,
    IGmGrantMoneyService gmGrantMoneyService,
    IGmFfaEventStartService gmFfaEventStartService,
    IGmSummonMonsterService gmSummonMonsterService,
    IGmBasicCommandService gmBasicCommandService,
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

        // tSort 501/502 -- Basic-tier (GmCommandTier.Basic) HIDE/SHOW self-commands
        // (Server/ts25zone/S04_MyWork04.cpp:933-958). No payload read. IGmBasicCommandService owns every
        // send/abort itself (a dedicated AvatarStatUpdateResponse notification, self-only, immediately followed
        // by the shared opcode-23 ack -- two packets for one request), so this branch never calls Respond().
        if (sort is 501 or 502)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleVisibilityAsync));
            await gmBasicCommandService.HandleVisibilityAsync(sort, packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 507 -- Basic-tier self-teleport-to-coordinate MOVE command
        // (Server/ts25zone/S04_MyWork04.cpp:1146-1164). IGmBasicCommandService owns every send/abort itself.
        if (sort == 507)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleSelfTeleportAsync));
            await gmBasicCommandService.HandleSelfTeleportAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 508 -- Basic-tier DIE command, force-invalidates a live monster instance by raw table index, not
        // a player (Server/ts25zone/S04_MyWork04.cpp:1165-1187). IGmBasicCommandService owns every send/abort
        // itself.
        if (sort == 508)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGmBasicCommandService.HandleForceKillMonsterAsync));
            await gmBasicCommandService.HandleForceKillMonsterAsync(packet.Data, zoneSession, zone,
                cancellationToken);
            return;
        }

        // tSort 510 -- Basic-tier TRIBE self-command (Server/ts25zone/S04_MyWork04.cpp:1203-1264). Sends NO
        // acknowledgment of any kind on any path -- a successful change forcibly disconnects the invoking GM
        // instead (the normal completion signal for this command); IGmBasicCommandService owns every
        // send/abort/disconnect itself.
        if (sort == 510)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleTribeChangeAsync));
            await gmBasicCommandService.HandleTribeChangeAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 511/512 -- Basic-tier EQUIP/UNEQUIP self-commands, writing an unread "special state" marker, no
        // item involved (Server/ts25zone/S04_MyWork04.cpp:1265-1298). IGmBasicCommandService owns every
        // send/abort itself.
        if (sort is 511 or 512)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGmBasicCommandService.HandleSelfSpecialStateAsync));
            await gmBasicCommandService.HandleSelfSpecialStateAsync(sort, packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 513 -- Basic-tier FIND command (Server/ts25zone/S04_MyWork04.cpp:1299-1323). See
        // IGmBasicCommandService's own remarks for why this is a deliberately simplified, process-local lookup
        // rather than legacy's genuinely cluster-wide blocking upstream round trip. IGmBasicCommandService owns
        // every send/abort itself (a dedicated GmCommandResponse immediately followed by the shared opcode-23
        // ack).
        if (sort == 513)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleFindAsync));
            await gmBasicCommandService.HandleFindAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        // tSort 514 -- Basic-tier CALL command, summons a named target to the invoker
        // (Server/ts25zone/S04_MyWork04.cpp:1324-1384). Only the ordinary single-target branch is implemented --
        // see IGmBasicCommandService's own remarks for the special-server mass-summon branch this project does
        // not implement. IGmBasicCommandService owns every send/abort itself.
        if (sort == 514)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleCallAsync));
            await gmBasicCommandService.HandleCallAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        // tSort 515 -- Basic-tier self-teleport-to-target's-position MOVE command, the reverse direction of CALL
        // (Server/ts25zone/S04_MyWork04.cpp:1385-1410). IGmBasicCommandService owns every send/abort itself.
        if (sort == 515)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleMoveToTargetAsync));
            await gmBasicCommandService.HandleMoveToTargetAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 516/517 -- Basic-tier NCHAT/YCHAT commands, marking a named target's "special state" marker to
        // 2/0 (Server/ts25zone/S04_MyWork04.cpp:1411-1468). Sends NO acknowledgment of any kind on any path --
        // IGmBasicCommandService owns every send/abort itself (never calls Respond()).
        if (sort is 516 or 517)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGmBasicCommandService.HandleTargetSpecialStateAsync));
            await gmBasicCommandService.HandleTargetSpecialStateAsync(sort, packet.Data, zoneSession, state,
                cancellationToken);
            return;
        }

        // tSort 518 -- Basic-tier KICK command, disconnects a named target's session
        // (Server/ts25zone/S04_MyWork04.cpp:1469-1486). IGmBasicCommandService owns every send/abort itself.
        if (sort == 518)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleKickAsync));
            await gmBasicCommandService.HandleKickAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        // tSort 520 -- Basic-tier TRIBEBANK command -- dead code behind a live tier gate
        // (Server/ts25zone/S04_MyWork04.cpp:1516-1540). Always the default-failure outcome, no mutation.
        // IGmBasicCommandService owns every send/abort itself.
        if (sort == 520)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleTribeBankAsync));
            await gmBasicCommandService.HandleTribeBankAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

        // tSort 521 -- Basic-tier LEVEL self-command (Server/ts25zone/S04_MyWork04.cpp:1541-1596).
        // IGmBasicCommandService owns every send/abort itself.
        if (sort == 521)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleLevelSetAsync));
            await gmBasicCommandService.HandleLevelSetAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 522 -- Basic-tier STR/DEX/VIT/INT stat-edit command -- dead code behind a live tier gate
        // (Server/ts25zone/S04_MyWork04.cpp:1597-1621). Always the default-failure outcome, no mutation.
        // IGmBasicCommandService owns every send/abort itself.
        if (sort == 522)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleStatEditAsync));
            await gmBasicCommandService.HandleStatEditAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

        // tSort 505/523 -- Admin-tier (GmCommandTier.Admin) "spawn-item" GM command
        // (Server/ts25zone/S04_MyWork04.cpp:1036-1095). No dedicated legacy wire opcode; multiplexed inside
        // this same generic envelope like every other tSort here. IGmCreateItemService owns every send/abort
        // itself (id-range/catalog-lookup failure sends the shared opcode-23 ack with the rejected code; a
        // stackable-item (iSort 2/99) downstream quantity-bound failure sends a distinct rejected code
        // mirroring legacy's own tResult=2; every other outcome -- including the non-stackable per-unit
        // branch's own downstream creation failure -- sends the accepted code, a confirmed legacy
        // control-flow defect specific to that branch which this project deliberately preserves), so this
        // branch never calls Respond() itself.
        if (sort is 505 or 523)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmCreateItemService.HandleAsync));
            await gmCreateItemService.HandleAsync(sort, packet.Data, zoneSession, state, zone, cancellationToken);
            return;
        }

        // tSort 509 -- Admin-tier "MAX" stat-cheat GM command (Server/ts25zone/S04_MyWork04.cpp:1188-1202). No
        // wire payload is ever read, and no response packet is ever composed for this selector on either the
        // privilege-gate-failure or the success path -- both end in a forced full-logout disconnect instead
        // (IGmMaxStatService owns that call itself), so this branch bypasses the shared response epilogue
        // entirely, matching legacy's own early return.
        if (sort == 509)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmMaxStatService.HandleAsync));
            await gmMaxStatService.HandleAsync(zoneSession, state, zone, cancellationToken);
            return;
        }

        // tSort 700 -- Admin-tier, unlabeled grant-pet-experience GM command
        // (Server/ts25zone/S04_MyWork04.cpp:2062-2083). No dedicated legacy wire opcode; multiplexed inside
        // this same generic envelope. IGmPetExperienceGrantService owns its own response composition (result
        // code is unconditionally accepted once the privilege gate passes), so this branch never calls
        // Respond() itself either.
        if (sort == 700)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmPetExperienceGrantService.HandleAsync));
            await gmPetExperienceGrantService.HandleAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        // tSort 503 -- Elevated-tier (GmCommandTier.Elevated) "[GM]-EXP" (grant experience to self) command
        // (Server/ts25zone/S04_MyWork04.cpp:959-1005). No dedicated legacy wire opcode; multiplexed inside this
        // same generic envelope. IGmExpGrantService owns its own response composition (result code is
        // unconditionally accepted once the tier gate passes), so this branch never calls Respond() itself.
        if (sort == 503)
        {
            if (!GmExpGrantPayload.TryRead(packet.Data, out var gmExpGrantPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmExpGrantPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmExpGrantService.HandleAsync));
            await gmExpGrantService.HandleAsync(gmExpGrantPayload, packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        // tSort 504 -- Elevated-tier "grant money" command (Server/ts25zone/S04_MyWork04.cpp:1006-1035). No
        // dedicated legacy wire opcode; multiplexed inside this same generic envelope. The 130-byte payload is
        // never read for this sub-command (the would-be parse is dead/commented-out legacy source) --
        // IGmGrantMoneyService owns its own response composition, so this branch never calls Respond() itself.
        if (sort == 504)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmGrantMoneyService.HandleAsync));
            await gmGrantMoneyService.HandleAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

        // tSort 333 -- Elevated-tier zone-wide FFA-start command (Server/ts25zone/S04_MyWork04.cpp:1097-1131).
        // No dedicated legacy wire opcode; multiplexed inside this same generic envelope (sitting outside the
        // 501-528 numeric block the other tier-10 sub-commands occupy -- a source-organization detail with no
        // behavioral effect). IGmFfaEventStartService owns its own response composition, so this branch never
        // calls Respond() itself.
        if (sort == 333)
        {
            if (!GmFfaEventStartPayload.TryRead(packet.Data, out var gmFfaEventStartPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmFfaEventStartPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmFfaEventStartService.HandleAsync));
            await gmFfaEventStartService.HandleAsync(gmFfaEventStartPayload, packet.Data, zoneSession,
                cancellationToken);
            return;
        }

        // tSort 506 -- Elevated-tier "moncall" (summon monster) command
        // (Server/ts25zone/S04_MyWork04.cpp:1133-1145). No dedicated legacy wire opcode; multiplexed inside
        // this same generic envelope. IGmSummonMonsterService owns its own response composition (result code
        // is unconditionally accepted once the tier gate passes), so this branch never calls Respond() itself.
        if (sort == 506)
        {
            if (!GmSummonMonsterPayload.TryRead(packet.Data, out var gmSummonMonsterPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmSummonMonsterPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmSummonMonsterService.HandleAsync));
            await gmSummonMonsterService.HandleAsync(gmSummonMonsterPayload, packet.Data, zoneSession, state, zone,
                cancellationToken);
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
