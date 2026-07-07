namespace Fenrir.Data.Abstractions.Game;

/// <summary>
///     game.EventLog.Category -- Trade/Currency/ItemCreate/ItemDestroy/Enchant/GmAction/Death/Session/
///     AccountSecurity/CashItemUse/ItemDrop. The table's own CHECK constraint allows 0-63 so new categories
///     can be added without a schema migration; this enum is the source of truth for the values actually in
///     use today.
/// </summary>
public enum EventLogCategory : byte
{
    Trade = 0,
    Currency = 1,
    ItemCreate = 2,
    ItemDestroy = 3,
    Enchant = 4,
    GmAction = 5,
    Death = 6,
    Session = 7,
    AccountSecurity = 8,

    /// <summary>
    ///     A cash-shop-style consumable item was used for its effect (e.g. a proxy-shop rental-extension
    ///     scroll) -- distinct from <see cref="ItemDestroy" /> (an item permanently deconstructed into a
    ///     byproduct via CZ_DESTROY_ITEM_SEND). First consumer:
    ///     Fenrir.Application.Game.Services.ZoneLifecycle.UseInventoryItemService's proxy-shop-rental-extension
    ///     branch.
    /// </summary>
    CashItemUse = 9,

    /// <summary>
    ///     A unique (non-stackable) item was manually dropped from inventory onto the ground (legacy
    ///     <c>GL_620_DROP_ITEM</c>) -- distinct from <see cref="ItemDestroy" /> (permanently deconstructed) and
    ///     <see cref="Trade" />/<see cref="Currency" /> (transferred to another character). Stackable-item drops
    ///     record no equivalent row, matching the legacy source's own asymmetry. First consumer:
    ///     Fenrir.Application.Game.Services.Inventory.InventoryToWorldDropService.
    /// </summary>
    ItemDrop = 10,

    /// <summary>
    ///     A guild-management action that moves (or, for disband, would have moved) a character's money:
    ///     create (EventCode 1), upgrade (EventCode 2), disband (EventCode 3, always DeltaMoney=0) -- legacy
    ///     <c>GL_617_GUILD_MONEY</c>. Distinct from <see cref="Currency" /> since this category is scoped to
    ///     the guild-management money movements specifically, not every currency delta in the game. Written
    ///     entirely from T-SQL (game.usp_Guild_CreateAndDebitMoney / usp_Guild_UpgradeAndDebitMoney /
    ///     usp_Guild_Disband each issue their own `EXEC game.usp_EventLog_Insert` in the same transaction as
    ///     the mutation -- see Database/Migrations/014_guild_money_event_log.sql), so there is no
    ///     Application-layer <see cref="IEventLogRepository.LogAsync" /> call site to point to for this one.
    /// </summary>
    GuildMoney = 11,

    /// <summary>
    ///     A player-triggered TimeExchange conversion (legacy <c>GL_851_PLAYTIME_EXCHANGE</c>, generic-action
    ///     tSort 237): accrued play-time-event minutes converted into teacher points + pet experience. First
    ///     consumer: Fenrir.Application.Game.Services.GenericAction.GenericActionService.TimeExchangeAsync.
    /// </summary>
    PlayTimeExchange = 12,

    /// <summary>
    ///     A generic inventory item was used via CZ_USE_INVENTORY_ITEM_SEND with no modeled domain-specific
    ///     effect of its own (legacy <c>GL_606_USE_INVENTORY_ITEM</c>, the same logging call shared by nearly
    ///     every item-use branch in the legacy dispatch) -- distinct from the narrower categories above
    ///     (<see cref="Currency" />, <see cref="CashItemUse" />, etc.) used by item-use branches that do have a
    ///     modeled monetary/gameplay effect worth its own category. First consumer:
    ///     Fenrir.Application.Game.Services.ZoneLifecycle.UseInventoryItemService's Teleport/Dungeon/Return
    ///     Scroll branch (item ids 1109/1224/1026), which per its legacy source has no effect beyond this
    ///     "before" usage log entry and an unconditional success reply.
    /// </summary>
    ItemUse = 13,

    /// <summary>
    ///     The offline/deputy-shop ("proxy shop") vertical: listing an item for sale (legacy
    ///     <c>GL_1000_PXSHOP_REG</c>), retrieving an unsold item or purchasing a listed one (legacy
    ///     <c>GL_1001_PXSHOP_ITEM</c>), and withdrawing a closed shop's accumulated earnings (legacy
    ///     <c>GL_1002_PXSHOP_MONEY</c>) -- a dedicated category rather than folding into <see cref="Trade" />
    ///     or <see cref="Currency" />, matching how <see cref="GuildMoney" />/<see cref="PlayTimeExchange" />
    ///     each got their own category instead of being shoehorned into a broader existing one. EventCode is
    ///     app-owned within this category: 1 = listed (per accepted slot), 2 = retrieved, 3 = purchased,
    ///     4 = earnings withdrawn. First consumers: Fenrir.Application.Game.Services.Commerce.
    ///     OpenShopStallService/UpdateProxyShopService/WithdrawProxyShopEarningsService.
    /// </summary>
    ProxyShop = 14
}

/// <summary>
///     game.EventLog -- the durable audit trail for domain-significant events. Two write paths exist for two
///     different call-site shapes:
///     <list type="bullet">
///         <item>
///             <see cref="LogAsync" /> (usp_EventLog_Insert) is the synchronous, transactional, high-stakes
///             path: trades, currency movements, item create/destroy, GM actions, deaths, session
///             lifecycle, account-security incidents. Call it directly for a standalone event; a future
///             mutation procedure that needs its audit row to live or die with the mutation itself should
///             instead issue the equivalent `EXEC game.usp_EventLog_Insert` as one more statement inside its
///             OWN transaction, rather than a separate C# round trip after the fact.
///         </item>
///         <item>
///             <see cref="BatchLogAsync" /> (usp_EventLog_InsertBatch) is the write-behind path for
///             high-frequency, low-stakes events (enchant/refine/socket rolls) that must not add a
///             synchronous DB round trip to every packet tick. See
///             <c>Fenrir.Data.WriteBehind.EventLogQueue</c> for the bounded-channel producer/consumer that
///             calls this in batches; it deliberately does NOT retry a failed flush (see that class's
///             remarks) -- this path is best-effort telemetry, never the durability-critical one.
///         </item>
///     </list>
/// </summary>
public interface IEventLogRepository
{
    /// <summary>
    ///     usp_EventLog_Insert: single-row synchronous write, for the high-stakes path where the caller needs
    ///     the write to happen now (and, if called from inside a caller's own stored procedure instead of from
    ///     C#, inside that caller's own ambient transaction). No domain failure path -- never throws a
    ///     registered admin.ErrorCatalog error, since an insert-only audit write has nothing to reject.
    /// </summary>
    public ValueTask LogAsync(
        short eventCode,
        EventLogCategory category,
        int? actorAccountId,
        int? actorCharacterId,
        int? targetAccountId,
        int? targetCharacterId,
        short? shardId,
        long? deltaMoney,
        long? deltaBigMoney,
        int? itemId,
        int? quantity,
        byte? outcome,
        string? payload,
        CancellationToken ct);

    /// <summary>
    ///     usp_EventLog_InsertBatch: multi-row write-behind flush for the high-frequency path. Never call with
    ///     an empty list -- SQL Server rejects a zero-row TVP outright; the implementation guards on
    ///     Count == 0 itself, same as every other batched write-behind method in this codebase, but callers
    ///     (EventLogQueue's drain loop) should still avoid building a batch for nothing to flush.
    /// </summary>
    public ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct);
}
