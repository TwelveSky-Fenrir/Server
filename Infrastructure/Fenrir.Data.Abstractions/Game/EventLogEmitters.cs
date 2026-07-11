namespace Fenrir.Data.Abstractions.Game;

/// <summary>
///     Centralized <see cref="IEventLogRepository" /> emission helpers for the six client-driven
///     inventory/money-transition audit families closed by workstream C20 ("GameLog/EventLog audit coverage
///     -- item/money-transition logging family", behavior contract citing GL_604/GL_606/GL_607/GL_619/
///     GL_622/GL_623/GL_849/GL_990-GL_997): cash-shop purchase, ground-item pickup, the loot-box elite-gain
///     third record, trade-window item/money staging, pet-inventory transfer, and big-money ("1B" unit)
///     ledger conversion.
/// </summary>
/// <remarks>
///     <para>
///         Placed in <c>Fenrir.Data.Abstractions.Game</c> (alongside <see cref="IEventLogRepository" />
///         itself), NOT in a <c>*.Services</c> "helpers" folder, because two of this contract's call sites
///         live in <c>Fenrir.Application.Game.Domain</c> (<c>LootBoxUseItemHandler</c>) and the rest in
///         <c>Fenrir.Application.Game.Services</c> (<c>GenericActionService</c>, <c>BuyCashItemService</c>) --
///         Domain must never depend on Services (see the vertical-slice layering rule,
///         Abstractions -&gt; Domain -&gt; Services -&gt; Handlers -&gt; Hosting), so any shared helper both call-site
///         tiers can use has to live no higher than Data.Abstractions, which both already reference directly.
///         Every family is exposed as an <see cref="IEventLogRepository" /> extension method so a call site
///         reads as <c>await eventLog.LogXAsync(...)</c> without needing to know the raw (Category, EventCode)
///         pair, the same "don't make the caller carry the magic numbers" goal this project's per-area
///         EventCode catalogs (<c>GmActionEventCodes</c>, <c>AccountSecurityEventCodes</c>) already serve for
///         single-project call sites -- this type exists because these six families span call sites in more
///         than one project.
///     </para>
///     <para>
///         <b>Deliberately NOT covered here</b> (per the contract's own Side effects section: every
///         structurally-similar sibling branch that legacy leaves silent): loose-money ground pickup, the
///         stack-merge ground-pickup branch, <c>ProcessForTradeToTrade</c> (both branches),
///         the stackable-merge branches of <c>ProcessForInventoryToTrade</c>/<c>ProcessForTradeToInventory</c>,
///         and <c>ProcessForPetInventoryToPetInventory</c> write NO legacy log record, and this class
///         provides no method for them either -- the contract flags whether Fenrir should close that
///         asymmetry as an unresolved open question, not something this class decides. Do not add a method
///         for one of those branches without first resolving that question.
///     </para>
/// </remarks>
public static class EventLogEmitters
{
    private const byte SuccessOutcome = 1;

    // ================================================================================================
    // (A) Cash-shop purchase -- legacy GL_604_BUY_CASH_ITEM (Server/ts25zone/S04_MyWork02.cpp:8226).
    // Category: EventLogCategory.CashShopPurchase (new member, wiring manifest -- see C20 report).
    // ================================================================================================

    /// <summary>
    ///     game.EventLog.EventCode for <see cref="LogCashShopPurchaseAsync" />. A single code is enough --
    ///     unlike NpcShopTrade's buy/sell pair, GL_604 only ever has one direction (spend cash, gain item).
    /// </summary>
    public const short CashShopPurchaseEventCode = 1;

    // ================================================================================================
    // (B) Ground-item pickup -- legacy GL_619_GAIN_ITEM (Server/ts25zone/S04_MyWork05.cpp:362) and,
    // conditionally, GL_607_GAIN_SIN_ITEM (:365) when the picked-up item is elite-typed.
    // Category: EventLogCategory.ItemPickup (new member, wiring manifest).
    // Deliberately NOT called for the Money (case 1) or Stacked (case 2) outcomes of
    // GroundItemPickupPolicy.Resolve -- only the fresh-slot ("Placed") outcome logs anything, matching
    // Server/ts25zone/S04_MyWork05.cpp:305-343 writing no record for either of those two branches.
    // ================================================================================================

    /// <summary>IELITE (Server/Header/Protocol/STRUCT.h:1658) -- an item Type at or above this is elite-grade.</summary>
    public const byte GroundItemEliteTypeThreshold = 4;

    /// <summary>game.EventLog.EventCode for the unconditional "gained item" record (GL_619_GAIN_ITEM).</summary>
    public const short GroundItemGainEventCode = 1;

    /// <summary>
    ///     game.EventLog.EventCode for the additional elite-gain record (GL_607_GAIN_SIN_ITEM), written
    ///     alongside <see cref="GroundItemGainEventCode" /> -- never instead of it -- only when the picked-up
    ///     item's type is elite-grade.
    /// </summary>
    public const short GroundItemGainEliteEventCode = 2;

    // ================================================================================================
    // (C) Loot-box elite-gain third record -- legacy GL_607_GAIN_SIN_ITEM (Server/ts25zone/S04_MyWork05.cpp:
    // 5409-5423), fired only for box ids 1035/1036/1037 when the granted reward is elite-typed. The
    // "before"/"after" GL_606 pair is ALREADY implemented (LootBoxUseItemHandler.BoxOpenBeforeEventCode=5 /
    // BoxOpenRewardEventCode=6, both under EventLogCategory.ItemUse); this is the still-missing third record,
    // reusing the same category. EventCode 7 is the next free value in ItemUse per LootBoxUseItemHandler's
    // own doc comment (1=teleport, 2/3=skill-grimoire, 4=stat-potion, 5/6=box-open before/reward) -- verify
    // that's still the high-water mark at integration time before relying on 7.
    // ================================================================================================

    /// <summary>
    ///     game.EventLog.EventCode for the box-open elite-gain audit record (GL_607_GAIN_SIN_ITEM, boxes 1035/1036/1037 +
    ///     elite reward).
    /// </summary>
    public const short BoxOpenEliteGainEventCode = 7;

    // ================================================================================================
    // (D) Trade-window item/money staging -- legacy GL_622_TRADESLOT_ITEM / GL_623_TRADESLOT_MONEY
    // (Server/ts25zone/S04_MyWork05.cpp:2136-2538). Category: EventLogCategory.Trade (existing, reused) --
    // EventCode 1/2 are ALREADY claimed by usp_CharacterTrade_Execute/usp_CharacterTradeCommit_ExecuteIdempotent
    // for the trade COMMIT's own GL_615/GL_616 audit rows (item transfer / money transfer respectively) --
    // staging is a DIFFERENT event (dragging into/out of the trade window offer, before commit), so it must
    // use different EventCodes within the same category: 3-6 are the next free values as of this session --
    // verify the current high-water mark in Trade before relying on that at integration time.
    // NOT called for: ProcessForTradeToTrade (writes no record in either branch, legacy parity, contract's
    // own open question), or the stackable-merge branches of ProcessForInventoryToTrade/ProcessForTradeToInventory
    // (only the whole-item "default" branch of each logs anything).
    // ================================================================================================

    /// <summary>
    ///     GL_622_TRADESLOT_ITEM, direction "inventory -&gt; trade window" (ProcessForInventoryToTrade's whole-item
    ///     branch).
    /// </summary>
    public const short TradeItemStagedToWindowEventCode = 3;

    /// <summary>
    ///     GL_622_TRADESLOT_ITEM, direction "trade window -&gt; inventory" (ProcessForTradeToInventory's whole-item
    ///     branch).
    /// </summary>
    public const short TradeItemStagedToInventoryEventCode = 4;

    /// <summary>GL_623_TRADESLOT_MONEY, direction "inventory money -&gt; trade money" (ProcessForInventoryMoneyToTradeMoney).</summary>
    public const short TradeMoneyStagedToWindowEventCode = 5;

    /// <summary>GL_623_TRADESLOT_MONEY, direction "trade money -&gt; inventory money" (ProcessForTradeMoneyToInventoryMoney).</summary>
    public const short TradeMoneyStagedToInventoryEventCode = 6;

    // ================================================================================================
    // (E) Pet-inventory transfer -- legacy GL_849_PET_INVENTORY (Server/ts25zone/S04_MyWork05.cpp:3806-3970).
    // Category: EventLogCategory.PetInventoryTransfer (new member, wiring manifest).
    // Legacy hardcodes the SAME direction-marker value (1) for both ProcessForInventoryToPetInventory and
    // ProcessForPetInventoryToInventory (:3873, :3967), so a legacy GL_849 row cannot tell the two directions
    // apart on its own (contract's own flagged open question). Fenrir DELIBERATELY resolves that question by
    // disambiguating via distinct EventCodes below -- a documented, parity-breaking improvement with no
    // downside, the same posture this project already takes elsewhere (see this class's own remarks and the
    // project's "close a legacy audit-trail ambiguity" precedent). NOT called for
    // ProcessForPetInventoryToPetInventory (legacy writes no record there either -- contract's own open
    // question, left unresolved, no method provided for it here).
    // ================================================================================================

    /// <summary>GL_849_PET_INVENTORY, direction "inventory -&gt; pet bag" (ProcessForInventoryToPetInventory).</summary>
    public const short PetInventoryToPetEventCode = 1;

    /// <summary>GL_849_PET_INVENTORY, direction "pet bag -&gt; inventory" (ProcessForPetInventoryToInventory).</summary>
    public const short PetInventoryFromPetEventCode = 2;

    // ================================================================================================
    // (F) Big-money ("1B" unit) ledger conversion -- legacy GL_990-GL_997 (Server/ts25zone/S04_MyWork05.cpp:
    // 3512-3804, UpperCom/S06_MyUpperCom05.cpp:1028-1074), all eight siblings unconditional, one distinct
    // legacy identifier per conversion pair/direction. Category: EventLogCategory.BigMoneyConversion (new
    // member, wiring manifest). Only the FIRST of the eight is individually named/cited in the C20 contract
    // (ProcessForInventoryMoneyTo1BInventoryMoney = GL_990, EventCode 1 below) -- the remaining seven
    // (GL_991-GL_997) are not individually distinguished by name in the cited source range, so EventCode
    // 2-8 are reserved as a generic ordered mapping (GL_991=2 ... GL_997=8) for whoever wires the actual
    // tSort 240-247 dispatch to assign per the real function order, rather than guessed at here.
    // ================================================================================================

    /// <summary>
    ///     GL_990 (ProcessForInventoryMoneyTo1BInventoryMoney): inventory Money -&gt; inventory BigMoney.
    ///     Non-proportionate by design -- always converts exactly +1 BigMoney unit regardless of the
    ///     requested transfer amount, while debiting the FULL requested amount from Money
    ///     (<c>wAvatar.aBigMoney += 1; wMoney -= tQuantity1;</c>, Server/ts25zone/S04_MyWork05.cpp:3540-3541).
    ///     <see cref="LogBigMoneyConversionAsync" />'s <c>fromLedgerDelta</c>/<c>toLedgerDelta</c> must carry
    ///     that true, non-proportionate pair verbatim -- do not "correct" it to look proportionate.
    /// </summary>
    public const short BigMoneyConversionEventCode1 = 1;

    /// <summary>GL_991 -- see this class's remarks: exact function mapping to be assigned by the dispatch implementer.</summary>
    public const short BigMoneyConversionEventCode2 = 2;

    /// <summary>GL_992 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode3 = 3;

    /// <summary>GL_993 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode4 = 4;

    /// <summary>GL_994 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode5 = 5;

    /// <summary>GL_995 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode6 = 6;

    /// <summary>GL_996 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode7 = 7;

    /// <summary>GL_997 -- see <see cref="BigMoneyConversionEventCode2" />'s remarks.</summary>
    public const short BigMoneyConversionEventCode8 = 8;

    /// <summary>
    ///     GL_604_BUY_CASH_ITEM parity: records item id/quantity/serial only. Deliberately never carries
    ///     <c>deltaMoney</c>/cash-cost -- per the C20 contract's Side effects (A), legacy computes
    ///     iCostSize/iBonusCostSize, passes both into GL_604_BUY_CASH_ITEM, and then discards them before
    ///     reaching any durable record (the commented-out <c>SendFormat</c> line is the only place that
    ///     would have written them; the live path delegates to a generic item-log template with no cost
    ///     field at all -- Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:313-318). Reproducing that omission
    ///     here is intentional legacy parity, not a Fenrir-side gap.
    /// </summary>
    public static ValueTask LogCashShopPurchaseAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, int itemId, int quantity, int serial, CancellationToken ct)
    {
        return eventLog.LogAsync(CashShopPurchaseEventCode, EventLogCategory.CashShopPurchase, accountId,
            characterId, null, null, null, null, null, itemId, quantity, SuccessOutcome,
            serial == 0 ? null : $"Serial={serial}", ct);
    }

    /// <summary>
    ///     Writes the fresh-slot ground-pickup audit pair: <see cref="GroundItemGainEventCode" /> always,
    ///     plus <see cref="GroundItemGainEliteEventCode" /> when <paramref name="itemType" /> is elite-grade
    ///     -- mirrors <c>ProcessForGetItem</c>'s default branch (Server/ts25zone/S04_MyWork05.cpp:344-370)
    ///     writing GL_619 unconditionally at :362 then GL_607 conditionally at :363-366.
    /// </summary>
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

    /// <summary>
    ///     Writes the loot-box elite-gain audit record. Callers should invoke this exactly when
    ///     <c>NoticeForBoxResolver.Decide(...).WriteEliteGainAudit</c> is true -- that flag already encodes
    ///     both the 1035/1036/1037 box-id gate and the elite-type gate (see
    ///     <c>NoticeForBoxResolver.EliteItemTypeThreshold</c>), independent of the (empty-today)
    ///     cluster-notice broadcast whitelist that separately gates <c>ShouldBroadcast</c>.
    /// </summary>
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

    public static ValueTask LogPetInventoryTransferAsync(this IEventLogRepository eventLog, int accountId,
        int characterId, bool intoPetBag, int petItemId, int serial, CancellationToken ct)
    {
        var eventCode = intoPetBag ? PetInventoryToPetEventCode : PetInventoryFromPetEventCode;
        return eventLog.LogAsync(eventCode, EventLogCategory.PetInventoryTransfer, accountId, characterId, null,
            null, null, null, null, petItemId, null, SuccessOutcome, serial == 0 ? null : $"Serial={serial}",
            ct);
    }

    /// <summary>
    ///     Writes one big-money conversion audit record. <paramref name="conversionEventCode" /> must be one
    ///     of the eight <c>BigMoneyConversionEventCodeN</c> constants above (not validated here -- the
    ///     dispatch implementer picks the right one per direction). <paramref name="fromLedgerDelta" />/
    ///     <paramref name="toLedgerDelta" /> are the raw signed deltas actually applied to the debited/
    ///     credited ledgers -- see <see cref="BigMoneyConversionEventCode1" />'s remarks for why these must
    ///     never be normalized to look proportionate.
    /// </summary>
    public static ValueTask LogBigMoneyConversionAsync(this IEventLogRepository eventLog, short conversionEventCode,
        int accountId, int characterId, long fromLedgerDelta, long toLedgerDelta, CancellationToken ct)
    {
        return eventLog.LogAsync(conversionEventCode, EventLogCategory.BigMoneyConversion, accountId,
            characterId, null, null, null, fromLedgerDelta, toLedgerDelta, null, null, SuccessOutcome, null, ct);
    }
}
