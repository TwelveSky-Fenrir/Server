namespace Fenrir.Data.Abstractions.Game;

public static class EventLogEmitters
{
    private const byte SuccessOutcome = 1;


    public const short CashShopPurchaseEventCode = 1;


    public const byte GroundItemEliteTypeThreshold = 4;

    public const short GroundItemGainEventCode = 1;

    public const short GroundItemGainEliteEventCode = 2;


    public const short BoxOpenEliteGainEventCode = 7;


    public const short TradeItemStagedToWindowEventCode = 3;

    public const short TradeItemStagedToInventoryEventCode = 4;

    public const short TradeMoneyStagedToWindowEventCode = 5;

    public const short TradeMoneyStagedToInventoryEventCode = 6;

    public const short TradeBigMoneyStagedToWindowEventCode = 7;

    public const short TradeBigMoneyStagedToInventoryEventCode = 8;


    public const short PetInventoryToPetEventCode = 1;

    public const short PetInventoryFromPetEventCode = 2;


    public const short BigMoneyConversionEventCode1 = 1;

    public const short BigMoneyConversionEventCode2 = 2;

    public const short BigMoneyConversionEventCode3 = 3;

    public const short BigMoneyConversionEventCode4 = 4;

    public const short BigMoneyConversionEventCode5 = 5;

    public const short BigMoneyConversionEventCode6 = 6;

    public const short BigMoneyConversionEventCode7 = 7;

    public const short BigMoneyConversionEventCode8 = 8;

    // Superseded as a production call path: BuyCashItemService no longer calls this directly (2026-07-12
    // transaction-composition-audit fix) -- the CashShopPurchase row is now nested inside
    // usp_Cash_DebitAndGrantItem's own transaction via its optional @AuditItemId/@AuditQuantity/@AuditSerial
    // params, so a crash between the debit and the audit write can no longer happen. Left in place as the
    // canonical description of this event's shape (event code, category, payload format).
    public static ValueTask LogCashShopPurchaseAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, int itemId, int quantity, int serial, CancellationToken ct)
    {
        return eventLog.LogAsync(CashShopPurchaseEventCode, EventLogCategory.CashShopPurchase, accountId,
            characterId, null, null, null, null, null, itemId, quantity, SuccessOutcome,
            serial == 0 ? null : $"Serial={serial}", ct);
    }

    public static async ValueTask LogGroundItemGainAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, int itemId, int quantity, byte itemType, CancellationToken ct)
    {
        await eventLog.LogAsync(GroundItemGainEventCode, EventLogCategory.ItemPickup, accountId, characterId,
            null, null, null, null, null, itemId, quantity, SuccessOutcome, null, ct).ConfigureAwait(false);

        if (itemType >= GroundItemEliteTypeThreshold)
            await eventLog.LogAsync(GroundItemGainEliteEventCode, EventLogCategory.ItemPickup, accountId,
                    characterId, null, null, null, null, null, itemId, quantity, SuccessOutcome, null, ct)
                .ConfigureAwait(false);
    }

    public static ValueTask LogBoxOpenEliteGainAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, int boxItemId, int rewardItemId, int rewardQuantity, CancellationToken ct)
    {
        return eventLog.LogAsync(BoxOpenEliteGainEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, rewardItemId, rewardQuantity, SuccessOutcome,
            $"Box={boxItemId};EliteReward={rewardItemId}", ct);
    }

    public static ValueTask LogTradeItemStagedAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, bool toTradeWindow, int itemId, int quantity, CancellationToken ct)
    {
        var eventCode = toTradeWindow ? TradeItemStagedToWindowEventCode : TradeItemStagedToInventoryEventCode;
        return eventLog.LogAsync(eventCode, EventLogCategory.Trade, accountId, characterId, null, null, null,
            null, null, itemId, quantity, SuccessOutcome, null, ct);
    }

    public static ValueTask LogTradeMoneyStagedAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, bool toTradeWindow, long amount, CancellationToken ct)
    {
        var eventCode = toTradeWindow ? TradeMoneyStagedToWindowEventCode : TradeMoneyStagedToInventoryEventCode;
        return eventLog.LogAsync(eventCode, EventLogCategory.Trade, accountId, characterId, null, null, null,
            amount, null, null, null, SuccessOutcome, null, ct);
    }

    public static ValueTask LogTradeBigMoneyStagedAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, bool toTradeWindow, long amount, long onHandBefore, long onHandAfter,
        long tradeOfferBefore, long tradeOfferAfter, CancellationToken ct)
    {
        var eventCode = toTradeWindow
            ? TradeBigMoneyStagedToWindowEventCode
            : TradeBigMoneyStagedToInventoryEventCode;
        return eventLog.LogAsync(eventCode, EventLogCategory.Trade, accountId, characterId, null, null, null,
            null, amount, null, null, SuccessOutcome,
            $"OnHand={onHandBefore}->{onHandAfter};Offer={tradeOfferBefore}->{tradeOfferAfter}", ct);
    }

    public static ValueTask LogPetInventoryTransferAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, bool intoPetBag, int petItemId, int serial, CancellationToken ct)
    {
        var eventCode = intoPetBag ? PetInventoryToPetEventCode : PetInventoryFromPetEventCode;
        return eventLog.LogAsync(eventCode, EventLogCategory.PetInventoryTransfer, accountId, characterId, null,
            null, null, null, null, petItemId, null, SuccessOutcome, serial == 0 ? null : $"Serial={serial}",
            ct);
    }

    public static ValueTask LogBigMoneyConversionAsync(this IEventLogRepository eventLog, short conversionEventCode,
        int? accountId, int characterId, long fromLedgerDelta, long toLedgerDelta, CancellationToken ct)
    {
        return eventLog.LogAsync(conversionEventCode, EventLogCategory.BigMoneyConversion, accountId,
            characterId, null, null, null, fromLedgerDelta, toLedgerDelta, null, null, SuccessOutcome, null, ct);
    }
}
