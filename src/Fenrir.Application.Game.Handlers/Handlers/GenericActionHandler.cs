using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Hotkeys;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

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
    IGmSetPvpPointService gmSetPvpPointService,
    IGmCallPvpService gmCallPvpService,
    IGmClearInventoryService gmClearInventoryService,
    IHotkeyActionService hotkeyActionService,
    IPetBagActionService petBagActionService,
    IBigMoneyTransferService bigMoneyTransferService,
    IBigMoneyUnitConversionService bigMoneyUnitConversionService,
    ILogger<GenericActionHandler> logger)
    : IAsyncPacketHandler<GenericActionRequest>
{
    private const int PetExperienceStatSort = 14;

    private const int HotkeyBindSortSkill = 1;

    private const int HotkeyBindSortEmoticon = 2;

    public async ValueTask HandleAsync(GenericActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
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
        IZoneSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var sort = packet.Sort;

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

        if (sort == 201)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.PickupGroundItemAsync));
            var result = await genericActionService.PickupGroundItemAsync(packet.Data, zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
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

        if (sort == 204)
        {
            var bindSort = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(0, 4));
            var bindValue = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(4, 4));
            var bindGrade = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(8, 4));
            var bindPage = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(12, 4));
            var bindIndex = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(16, 4));

            if (bindSort is not (HotkeyBindSortSkill or HotkeyBindSortEmoticon))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} rejected, unsupported hotkey binding sort {BindSort}",
                    zoneSession.SessionId, characterId, sort, bindSort);
                Respond(session, zoneSession, sort, packet.Data, GenericActionResult.Aborted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    bindSort == HotkeyBindSortSkill
                        ? nameof(IHotkeyActionService.BindSkillAsync)
                        : nameof(IHotkeyActionService.BindEmoticonAsync));

            var bindResult = bindSort == HotkeyBindSortSkill
                ? await hotkeyActionService.BindSkillAsync(zone, state, characterId, bindValue, bindGrade,
                    bindPage, bindIndex, cancellationToken)
                : await hotkeyActionService.BindEmoticonAsync(zone, state, characterId, bindValue, bindPage,
                    bindIndex, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, bindResult);
            return;
        }

        if (sort == 205)
        {
            var unbindPage = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(0, 4));
            var unbindIndex = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(4, 4));
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IHotkeyActionService.UnbindAsync));
            var unbindResult = await hotkeyActionService.UnbindAsync(zone, state, characterId, unbindPage,
                unbindIndex, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, unbindResult);
            return;
        }

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

            var result = sort == 215
                ? await genericActionService.BuyFromNpcShopAsync(zone, state, zoneSession.AccountId!.Value,
                    characterId, shopMove, cancellationToken)
                : await genericActionService.SellToNpcShopAsync(zone, state, zoneSession.AccountId!.Value,
                    characterId, shopMove, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

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

            var runeResult = await runeStoneCraftService.CraftAsync(runeMove.Page1, runeMove.Index1,
                runeMove.Page2, runeMove.Index2, runeMove.XPost2, 0,
                true, zone, state, characterId, cancellationToken);
            RespondRune(session, zoneSession, sort, packet.Data, runeResult);
            return;
        }

        if (sort == 235)
        {
            var requestedUnits = BinaryPrimitives.ReadInt32LittleEndian(packet.Data.AsSpan(0, 4));
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method} (requestedUnits {RequestedUnits})",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGenericActionService.ExchangeMeritForContributionPointsAsync), requestedUnits);
            var result = await genericActionService.ExchangeMeritForContributionPointsAsync(requestedUnits, zone,
                state, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        if (sort == 236)
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} disconnects unconditionally, no response sent (Server/ts25zone/S04_MyWork04.cpp:849-853 live LNW33 case 236: Quit())",
                zoneSession.SessionId, characterId, sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (sort == 239)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} is a legacy no-op success",
                    zoneSession.SessionId, characterId, sort);
            Respond(session, zoneSession, sort, packet.Data, GenericActionResult.Succeeded);
            return;
        }

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

        if (sort is 218 or 219 or 220)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferTradeItemAsync));
            var result = await genericActionService.TransferTradeItemAsync(sort, packet.Data, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        if (sort is 221 or 222)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGenericActionService.TransferTradeMoneyAsync));
            var result = await genericActionService.TransferTradeMoneyAsync(sort, packet.Data, state, characterId,
                cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, result);
            return;
        }

        if (sort is 211 or 253 or 214 or 216)
        {
            if (!DefaultPData.TryRead(packet.Data, out var hotkeyMove))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed DefaultPData payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to hotkey family",
                    zoneSession.SessionId, characterId, sort);

            var secondInventoryPageActive = state.InventoryDate >= GameDate.Today();
            var hotkeyResult = sort switch
            {
                211 or 253 => await hotkeyActionService.BindItemAsync(zone, state, characterId, hotkeyMove,
                    secondInventoryPageActive, cancellationToken),
                214 => await hotkeyActionService.WithdrawItemAsync(zone, state, characterId, hotkeyMove,
                    secondInventoryPageActive, cancellationToken),
                _ => await hotkeyActionService.RearrangeAsync(zone, state, characterId, hotkeyMove,
                    cancellationToken)
            };
            Respond(session, zoneSession, sort, packet.Data, hotkeyResult);
            return;
        }

        if (sort is 254 or 255 or 256)
        {
            if (!DefaultPData.TryRead(packet.Data, out var petBagMove))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed DefaultPData payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to pet-bag family",
                    zoneSession.SessionId, characterId, sort);

            var petBagUpperHalfEntitlementActive = state.PetBagDate >= GameDate.Today();
            var secondInventoryPageActive = state.InventoryDate >= GameDate.Today();
            var petBagResult = sort switch
            {
                254 => await petBagActionService.DepositAsync(zone, state, characterId, petBagMove,
                    petBagUpperHalfEntitlementActive, secondInventoryPageActive, cancellationToken),
                255 => await petBagActionService.WithdrawAsync(zone, state, characterId, petBagMove,
                    petBagUpperHalfEntitlementActive, secondInventoryPageActive, cancellationToken),
                _ => await petBagActionService.RearrangeAsync(zone, state, characterId, petBagMove,
                    petBagUpperHalfEntitlementActive, cancellationToken)
            };
            Respond(session, zoneSession, sort, packet.Data, petBagResult);
            return;
        }

        if (sort is 241 or 244 or 242 or 245 or 240 or 243)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to BigMoney family",
                    zoneSession.SessionId, characterId, sort);

            var bigMoneyResult = sort switch
            {
                241 or 244 => await bigMoneyTransferService.TransferStoreAsync(sort, packet.Data, characterId,
                    cancellationToken),
                242 or 245 => await bigMoneyTransferService.TransferSaveAsync(sort, packet.Data,
                    zoneSession.AccountId!.Value, characterId, cancellationToken),
                _ => await bigMoneyTransferService.TransferTradeAsync(sort, packet.Data, zone, state,
                    zoneSession.AccountId!.Value, characterId, cancellationToken)
            };
            Respond(session, zoneSession, sort, packet.Data, bigMoneyResult);
            return;
        }

        if (sort is 246 or 247)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IBigMoneyUnitConversionService.ConvertAsync));

            var conversionResult = await bigMoneyUnitConversionService.ConvertAsync(sort, packet.Data, zone, state,
                zoneSession.AccountId!.Value, characterId, cancellationToken);
            Respond(session, zoneSession, sort, packet.Data, conversionResult);
            return;
        }

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

        if (sort == 508)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGmBasicCommandService.HandleForceKillMonsterAsync));
            await gmBasicCommandService.HandleForceKillMonsterAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

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

        if (sort == 513)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleFindAsync));
            await gmBasicCommandService.HandleFindAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        if (sort == 514)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleCallAsync));
            await gmBasicCommandService.HandleCallAsync(packet.Data, zoneSession, state, zone, cancellationToken);
            return;
        }

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

        if (sort == 528)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort,
                    nameof(IGmBasicCommandService.HandleMoveToPositionAsync));
            await gmBasicCommandService.HandleMoveToPositionAsync(packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

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

        if (sort == 518)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleKickAsync));
            await gmBasicCommandService.HandleKickAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

        if (sort == 520)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleTribeBankAsync));
            await gmBasicCommandService.HandleTribeBankAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

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

        if (sort == 522)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmBasicCommandService.HandleStatEditAsync));
            await gmBasicCommandService.HandleStatEditAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

        if (sort == 598)
        {
            if (!GmSetPvpPointPayload.TryRead(packet.Data, out var gmSetPvpPointPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmSetPvpPointPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmSetPvpPointService.HandleAsync));
            await gmSetPvpPointService.HandleAsync(gmSetPvpPointPayload, packet.Data, zoneSession, cancellationToken);
            return;
        }

        if (sort == 599)
        {
            if (!GmCallPvpPayload.TryRead(packet.Data, out var gmCallPvpPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmCallPvpPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmCallPvpService.HandleAsync));
            await gmCallPvpService.HandleAsync(gmCallPvpPayload, packet.Data, zoneSession, cancellationToken);
            return;
        }

        if (sort == 701)
        {
            if (!GmClearInventoryPayload.TryRead(packet.Data, out var gmClearInventoryPayload))
            {
                logger.LogInformation(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} aborted, malformed GmClearInventoryPayload payload",
                    zoneSession.SessionId, characterId, sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmClearInventoryService.HandleAsync));
            await gmClearInventoryService.HandleAsync(gmClearInventoryPayload, packet.Data, zoneSession, state, zone,
                cancellationToken);
            return;
        }

        if (sort is 505 or 523)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmCreateItemService.HandleAsync));
            await gmCreateItemService.HandleAsync(sort, packet.Data, zoneSession, state, zone, cancellationToken);
            return;
        }

        if (sort == 509)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmMaxStatService.HandleAsync));
            await gmMaxStatService.HandleAsync(zoneSession, state, zone, cancellationToken);
            return;
        }

        if (sort == 700)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmPetExperienceGrantService.HandleAsync));
            await gmPetExperienceGrantService.HandleAsync(packet.Data, zoneSession, state, cancellationToken);
            return;
        }

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

        if (sort == 504)
        {
            if (debugEnabled)
                logger.LogDebug(
                    "Session {SessionId} character {CharacterId}: GenericAction Sort {Sort} dispatched to {Method}",
                    zoneSession.SessionId, characterId, sort, nameof(IGmGrantMoneyService.HandleAsync));
            await gmGrantMoneyService.HandleAsync(packet.Data, zoneSession, cancellationToken);
            return;
        }

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

    private void Respond(IPacketSession session, IZoneSession zoneSession, int sort, byte[] data,
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

    private void RespondDrop(IPacketSession session, IZoneSession zoneSession, int sort, byte[] data,
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

    private void RespondRune(IPacketSession session, IZoneSession zoneSession, int sort, byte[] data,
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
