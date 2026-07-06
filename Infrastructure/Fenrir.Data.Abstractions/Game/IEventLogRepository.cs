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
    GuildMoney = 11
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
