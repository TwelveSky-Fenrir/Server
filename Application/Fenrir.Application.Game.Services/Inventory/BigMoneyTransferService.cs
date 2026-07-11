using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;
using IBigMoneyRepository = Fenrir.Data.Abstractions.Inventory.IBigMoneyRepository;

namespace Fenrir.Application.Game.Services.Inventory;

public sealed class BigMoneyTransferService(
    IBigMoneyRepository bigMoney,
    IEventLogRepository eventLog,
    TradeRegistry trades,
    ZoneRegistry zones,
    ILogger<BigMoneyTransferService> logger)
    : IBigMoneyTransferService
{
    public async ValueTask<GenericActionResult> TransferStoreAsync(int sort, byte[] data, int characterId,
        CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Store-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Store-transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 241;
        var deltaInventoryBigMoney = isDeposit ? -move.Quantity1 : move.Quantity1;
        var deltaStoreBigMoney = isDeposit ? move.Quantity1 : -move.Quantity1;

        try
        {
            await bigMoney.AdjustInventoryStoreAsync(characterId, deltaInventoryBigMoney, deltaStoreBigMoney,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} BigMoney-Store-transfer AdjustInventoryStoreAsync failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} BigMoney-Store-transfer applied: isDeposit={IsDeposit}, amount {Quantity1}",
            characterId, isDeposit, move.Quantity1);

        var storeEventCode = isDeposit
            ? EventLogEmitters.BigMoneyConversionEventCode5
            : EventLogEmitters.BigMoneyConversionEventCode6;
        var (storeFromDelta, storeToDelta) = isDeposit
            ? (deltaInventoryBigMoney, deltaStoreBigMoney)
            : (deltaStoreBigMoney, deltaInventoryBigMoney);
        await eventLog.LogBigMoneyConversionAsync(storeEventCode, accountId: null, characterId, storeFromDelta,
            storeToDelta, cancellationToken);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TransferSaveAsync(int sort, byte[] data, int accountId,
        int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Save-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Save-transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 242;
        var deltaInventoryBigMoney = isDeposit ? -move.Quantity1 : move.Quantity1;
        var deltaVaultBigMoney = isDeposit ? move.Quantity1 : -move.Quantity1;

        try
        {
            await bigMoney.AdjustInventorySaveAsync(characterId, deltaInventoryBigMoney, accountId,
                deltaVaultBigMoney, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} BigMoney-Save-transfer AdjustInventorySaveAsync failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} BigMoney-Save-transfer applied: isDeposit={IsDeposit}, amount {Quantity1}",
            characterId, isDeposit, move.Quantity1);

        var saveEventCode = isDeposit
            ? EventLogEmitters.BigMoneyConversionEventCode7
            : EventLogEmitters.BigMoneyConversionEventCode8;
        var (saveFromDelta, saveToDelta) = isDeposit
            ? (deltaInventoryBigMoney, deltaVaultBigMoney)
            : (deltaVaultBigMoney, deltaInventoryBigMoney);
        await eventLog.LogBigMoneyConversionAsync(saveEventCode, accountId, characterId, saveFromDelta, saveToDelta,
            cancellationToken);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TransferTradeAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Trade-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Trade-transfer aborted: no active trade session (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        var side = trade.SideOf(characterId);
        var toTradeWindow = sort == 240;
        var onHandBefore = state.BigMoney;
        var offerBefore = side.BigMoney;

        var resolved = toTradeWindow
            ? TradeBigMoneyPlacementResolver.ResolveToTradeOffer(side.MenuState, onHandBefore, offerBefore,
                move.Quantity1)
            : TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(side.MenuState, offerBefore, onHandBefore,
                move.Quantity1);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Trade-transfer aborted: {Outcome} (sort {Sort}, amount {Quantity1})",
                characterId, resolved.Outcome, sort, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        side.BigMoney = (int)resolved.NewTradeOfferBigMoney;

        var bigMoneyDelta = toTradeWindow ? -move.Quantity1 : move.Quantity1;
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, BigMoneyDelta: bigMoneyDelta), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped BigMoney-Trade-transfer mirror for character {CharacterId}",
                zone.MapId, characterId);

        await eventLog.LogTradeBigMoneyStagedAsync(accountId, characterId, toTradeWindow, move.Quantity1,
            onHandBefore, onHandBefore + bigMoneyDelta, offerBefore, resolved.NewTradeOfferBigMoney,
            cancellationToken);

        TradeOfferResyncNotifier.TryNotifyOpponent(trades, zones, characterId);

        logger.LogInformation(
            "Character {CharacterId} BigMoney-Trade-transfer applied: toTradeWindow={ToTradeWindow}, amount {Quantity1}, OnHand {OnHandBefore}->{OnHandAfter}, Offer {OfferBefore}->{OfferAfter}",
            characterId, toTradeWindow, move.Quantity1, onHandBefore, onHandBefore + bigMoneyDelta, offerBefore,
            resolved.NewTradeOfferBigMoney);

        return GenericActionResult.Succeeded;
    }
}
