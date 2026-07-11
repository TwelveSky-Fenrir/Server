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

    /// <summary>
    ///     A GM command's effect on a character, any tier -- EventCode is app-owned within this category per
    ///     <c>Fenrir.Application.Game.Services.Gm.GmActionEventCodes</c> (that project's own locally-scoped
    ///     catalog, mirroring <c>Fenrir.Application.Login.Services.AccountSecurity.AccountSecurityEventCodes</c>'
    ///     convention). Admin-tier (<c>GmCommandTier.Admin</c>) first consumers: <c>GmMaxStatService</c> (tSort
    ///     509, "MAX" stat-cheat) and <c>GmPetExperienceGrantService</c> (tSort 700, grant-pet-experience) --
    ///     both a Fenrir-authored audit trail added by project decision, since neither command's own cited
    ///     legacy body (Server/ts25zone/S04_MyWork04.cpp:1188-1202 / :2062-2083) writes any audit-log call
    ///     itself. The third Admin-tier command, <c>GmCreateItemService</c> (tSort 505/523, spawn-item), logs
    ///     to <see cref="ItemCreate" /> instead, since a real legacy audit-log call for that command already
    ///     exists to map onto (Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:289-305). Basic-tier
    ///     (<c>GmCommandTier.Basic</c>) consumers: <c>IGmBasicCommandService</c>'s DIE (tSort 508), CALL
    ///     (tSort 514), KICK (tSort 518), and the shared NCHAT/YCHAT (tSort 516/517) log point -- these four
    ///     DO have a real legacy audit-log call each (Server/ts25zone/S04_MyWork04.cpp:1165-1187,1324-1384,
    ///     1411-1468,1469-1486), unlike the two Admin-tier project-decision consumers above.
    /// </summary>
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
    ProxyShop = 14,

    /// <summary>
    ///     A cosmetic entitlement is permanently removed from the character's active collection -- legacy
    ///     <c>CZ_COSTUME_STATE_SEND</c> Sort 5 ("Delete"): the wardrobe slot is cleared and, as a make-good, a
    ///     plain inventory item is granted back (<c>CostumeStateResolver.ResultKind.ReturnToInventorySuccess</c>)
    ///     -- distinct from <see cref="ItemCreate" /> since nothing new is minted, an existing entitlement is
    ///     merely converted back to its inventory form. The mount equivalent (op87 Sort 5,
    ///     <c>CZ_ANIMAL_STATE_SEND</c>) does NOT use this category -- see <see cref="MountAttribute" />
    ///     instead, since Sort 5 grants CP as compensation rather than returning an inventory item, and was
    ///     previously left an out-of-scope disconnect specifically to close a "delete mount id 0" free-CP
    ///     quirk (see <c>MountStateResolver</c>'s own remarks for how that quirk is now closed by a narrower,
    ///     targeted guard instead of leaving the whole sub-action unimplemented). First consumer:
    ///     Fenrir.Application.Game.Services.BuffsMountsCosmetics.CostumeStateService's return-to-inventory
    ///     branch.
    /// </summary>
    CosmeticDelete = 15,

    /// <summary>
    ///     A player sold an item to, or bought an item from, an NPC shop -- legacy <c>GL_621_NSHOP_ITEM</c>
    ///     (ServerDocs/12_ts25zone/07_MyWork05_Helpers.md:163-171 names it as the dedicated audit call the
    ///     NPC-shop family (<c>ProcessForInventoryToNPCShop</c>/<c>ProcessForNPCShopToInventory</c>,
    ///     Server/ts25zone/S04_MyWork05.cpp:1398-1542/1716-2029) issues before returning; that documentation
    ///     does not pin the call to an exact line number, so none is cited here). EventCode is app-owned
    ///     within this category: 1 = sold to the shop, 2 = bought from the shop. A dedicated category rather
    ///     than folding into <see cref="Currency" />, matching how <see cref="ProxyShop" />/
    ///     <see cref="GuildMoney" /> each got their own category instead of being shoehorned into a broader
    ///     existing one. First consumer: Fenrir.Application.Game.Services.GenericAction.GenericActionService's
    ///     SellToNpcShopAsync/BuyFromNpcShopAsync (backing
    ///     Fenrir.Application.Game.Domain.World.Npcs.NpcShopPolicy's ResolveSell/ResolveBuy).
    /// </summary>
    NpcShopTrade = 16,

    /// <summary>
    ///     A non-stackable item moved between a character's Inventory and its own Store/coffre
    ///     (CZ_PROCESS_DATA_SEND tSort 223/250 deposit, 224/248 withdraw; legacy <c>GL_624_STORESLOT_ITEM</c>).
    ///     EventCode is app-owned within this category, matching <see cref="NpcShopTrade" />'s own convention:
    ///     1 = deposited into the store, 2 = withdrawn to inventory. Never emitted for a stackable transfer or
    ///     a store-to-store rearrange, matching StoreItemTransferPolicy's own <c>IsNonStackableTransfer</c>
    ///     remarks. First consumer: GenericActionService.TransferStoreItemAsync.
    /// </summary>
    StoreSlotItem = 17,

    /// <summary>
    ///     A non-stackable item moved between a character's Inventory and its account's shared Save/vault
    ///     (CZ_PROCESS_DATA_SEND tSort 228/251 deposit, 229/249 withdraw; legacy <c>GL_626_SAVESLOT_ITEM</c>).
    ///     Same EventCode convention as <see cref="StoreSlotItem" />. Never emitted for a stackable transfer
    ///     or a bank-to-bank rearrange. First consumer: GenericActionService.TransferBankItemAsync.
    /// </summary>
    SaveSlotItem = 18,

    /// <summary>
    ///     Money moved between a character's wallet and its own Store/coffre money pool (CZ_PROCESS_DATA_SEND
    ///     tSort 226 deposit, 227 withdraw; legacy <c>GL_625_STORESLOT_MONEY</c>). Same EventCode convention
    ///     as <see cref="StoreSlotItem" />. First consumer: GenericActionService.TransferStoreMoneyAsync.
    /// </summary>
    StoreSlotMoney = 19,

    /// <summary>
    ///     Money moved between a character's wallet and its account's shared Save/vault money pool
    ///     (CZ_PROCESS_DATA_SEND tSort 231 deposit, 232 withdraw; legacy <c>GL_627_SAVESLOT_MONEY</c>). Same
    ///     EventCode convention as <see cref="StoreSlotItem" />. First consumer:
    ///     GenericActionService.TransferBankMoneyAsync.
    /// </summary>
    SaveSlotMoney = 20,

    /// <summary>
    ///     CZ_ANIMAL_STATE_SEND (op87) mount-attribute management: Sort 5 (Delete Mount, a +250 CP
    ///     compensation grant) / Sort 7 (Delete Rolled Attribute, item 1225 consumed) -- legacy
    ///     Server/ts25zone/S04_MyWork02.cpp:11907-11919/:11977-11991. EventCode is app-owned within this
    ///     category: 1 = delete mount, 2 = delete rolled attribute. Sort 6 (Convert)/8 (Transfer) never reach
    ///     a loggable success path yet -- see <c>MountStateResolver</c>'s own remarks for the uncataloged
    ///     roll/transfer formula this is blocked on. <see cref="IEventLogRepository.LogAsync" />'s
    ///     <c>deltaMoney</c> parameter carries the CP delta for EventCode 1 (there is no dedicated
    ///     Contribution-Points column on game.EventLog); Sort 5 does not touch actual wallet money despite
    ///     the legacy audit line also displaying it. First consumer:
    ///     Fenrir.Application.Game.Services.BuffsMountsCosmetics.MountStateService.
    /// </summary>
    MountAttribute = 21,

    /// <summary>
    ///     A cash-shop purchase (legacy GL_604_BUY_CASH_ITEM, CZ_BUY_CASH_ITEM_SEND op42) -- spend cash
    ///     currency, gain an item. Distinct from <see cref="CashItemUse" /> (using an already-owned cash item
    ///     for its effect) since this is the purchase itself. EventCode is app-owned within this category
    ///     (see <c>EventLogEmitters.CashShopPurchaseEventCode</c>). First consumer:
    ///     Fenrir.Application.Game.Services.Commerce.BuyCashItemService (C20 audit-log workstream).
    /// </summary>
    CashShopPurchase = 22,

    /// <summary>
    ///     A player picked up a ground item into inventory (legacy GL_619_GAIN_ITEM, and conditionally
    ///     GL_607_GAIN_SIN_ITEM for an elite-typed pickup) -- the pickup counterpart to <see cref="ItemDrop" />.
    ///     Only the fresh-slot outcome of GroundItemPickupPolicy.Resolve logs anything; the Money and Stacked
    ///     outcomes write no record, matching legacy's own silent branches. EventCode is app-owned within
    ///     this category (see <c>EventLogEmitters.GroundItemGainEventCode</c> /
    ///     <c>GroundItemGainEliteEventCode</c>). First consumer:
    ///     Fenrir.Application.Game.Services.GenericAction.GenericActionService.PickupGroundItemAsync (C20
    ///     audit-log workstream).
    /// </summary>
    ItemPickup = 23,

    /// <summary>
    ///     A non-stackable item moved between a character's Inventory and its equipped pet's own bag (legacy
    ///     GL_849_PET_INVENTORY, CZ_PROCESS_DATA_SEND tSort 254 inventory-to-pet / 255 pet-to-inventory; tSort
    ///     256 pet-to-pet writes no legacy record and has no EventCode here either). Legacy hardcodes the same
    ///     direction marker for both 254/255 -- Fenrir deliberately disambiguates via distinct EventCodes (see
    ///     <c>EventLogEmitters.PetInventoryToPetEventCode</c> / <c>PetInventoryFromPetEventCode</c>). NOT YET
    ///     WIRED as of this member's addition -- no GenericActionHandler/GenericActionService dispatch exists
    ///     for tSort 254-256 yet (C20 audit-log workstream; blocked on that base feature's own dispatch).
    /// </summary>
    PetInventoryTransfer = 24,

    /// <summary>
    ///     A big-money ("1B" unit) ledger conversion -- inventory Money/BigMoney &lt;-&gt; Store/Bank BigMoney,
    ///     or Money-to-BigMoney unit conversion (legacy GL_990-GL_997, CZ_PROCESS_DATA_SEND tSort 240-247).
    ///     Eight distinct EventCodes, one per legacy identifier (see
    ///     <c>EventLogEmitters.BigMoneyConversionEventCode1</c>-<c>8</c>).
    /// </summary>
    /// <remarks>
    ///     The full tSort/GL/EventCode mapping is now CONFIRMED for all eight siblings (re-read pass,
    ///     independently cross-checked against both each body's own <c>mGAMELOG.GL_99x_...(...)</c> call in
    ///     Server/ts25zone/S04_MyWork05.cpp:3512-3804 and its own literal <c>mLogSort = 99N</c> assignment in
    ///     UpperCom/S06_MyUpperCom05.cpp:1028-1074 -- see <c>EventLogEmitters</c>'s own remarks table for the
    ///     full mapping and every constant's individual citation). Dispatch/wiring status, updated by this
    ///     pass:
    ///     <list type="bullet">
    ///         <item>
    ///             tSort 241/244 (Inventory&lt;-&gt;Store) and 242/245 (Inventory&lt;-&gt;Save/vault) ARE
    ///             dispatched, via <c>IBigMoneyTransferService</c>/<c>BigMoneyTransferService</c>
    ///             (Application/Fenrir.Application.Game.Services/Inventory/BigMoneyTransferService.cs, called
    ///             from <c>GenericActionHandler.DispatchAsync</c>'s BigMoney branch), and that service now
    ///             calls <see cref="IEventLogRepository" />/<c>EventLogEmitters.LogBigMoneyConversionAsync</c>
    ///             for all four (EventCode5/6/7/8) -- this category now has production writers for this
    ///             half of the family.
    ///         </item>
    ///         <item>
    ///             tSort 240/243 (trade-offer BigMoney) and 246/247 (Money&lt;-&gt;BigMoney unit conversion,
    ///             <c>BigMoneyUnitConversionPolicy</c>) still have NO dispatch of any kind anywhere in this
    ///             codebase (their only callers remain their own unit tests) -- EventCode1-4 remain unused
    ///             by any production call site. This is an open TODO tracked against whichever future pass
    ///             wires the trade-offer BigMoney subsystem and the 246/247 unit-conversion dispatch, not an
    ///             unresolved identifier mapping (the mapping itself is settled).
    ///         </item>
    ///     </list>
    /// </remarks>
    BigMoneyConversion = 25,

    /// <summary>
    ///     An anti-cheat tamper guard rejected a client-originated action -- EventCode is the offending guard's
    ///     own offense enum value (app-owned per guard, matching how <see cref="GmAction" />'s EventCode is
    ///     app-owned per <c>GmActionEventCodes</c>). First consumer:
    ///     <c>Fenrir.Application.Game.Domain.World.Zone.PlayerLifecycle.cs</c>'s D2 skill-cast tamper guard
    ///     (<c>Fenrir.Application.Game.Domain.AntiCheat.SkillCastGuard</c>), whose
    ///     <c>SkillCastOffense</c> byte value (HotkeyMismatch=1/LearnedSkillMissing=2/BonusGradeMismatch=3/
    ///     SkillHack1=4) is written directly as EventCode. Written via the best-effort
    ///     <see cref="IEventLogQueue" /> write-behind path, not the durable <see cref="IEventLogRepository.LogAsync" /> path
    ///     -- the
    ///     zone tick thread that detects an offense must never block on SQL I/O (same constraint every other
    ///     <c>Zone</c> event-log producer already works around, see <c>Zone.PlayerLifecycle.cs</c>'s
    ///     <c>PendingDeathEventLog</c>); a dropped row under sustained DB unavailability is an accepted
    ///     trade-off for a signal whose real-time enforcement (the disconnect itself) does not depend on the
    ///     audit row landing.
    /// </summary>
    AntiCheat = 26
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
