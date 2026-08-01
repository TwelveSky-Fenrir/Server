namespace Fenrir.Data.Abstractions.Game;

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

    CashItemUse = 9,

    ItemDrop = 10,

    GuildMoney = 11,

    PlayTimeExchange = 12,

    ItemUse = 13,

    ProxyShop = 14,

    CosmeticDelete = 15,

    NpcShopTrade = 16,

    StoreSlotItem = 17,

    SaveSlotItem = 18,

    StoreSlotMoney = 19,

    SaveSlotMoney = 20,

    MountAttribute = 21,

    CashShopPurchase = 22,

    ItemPickup = 23,

    PetInventoryTransfer = 24,

    BigMoneyConversion = 25,

    AntiCheat = 26
}

public interface IEventLogRepository
{
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

    public ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct);
}
