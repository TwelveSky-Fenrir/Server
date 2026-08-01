using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Data.Characters;

public sealed record CharacterRepository(ICaeriusNetDbContext Db) : ICharacterRepository
{
    public const byte NoContainer = 255;

    public async ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetByAccount", 3)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterSummaryDto>(sp, ct);
    }

    public async ValueTask<CharacterAccountRosterBundle> GetAccountRosterAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetAccountRoster", 192)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        var (characters, items, petBagSlots, costumeSlots) = await Db
            .QueryMultipleReadOnlyCollectionAsync<CharacterRosterDto, CharacterRosterItemDto,
                CharacterRosterPetBagSlotDto, CharacterRosterCostumeSlotDto>(sp, ct);

        return new CharacterAccountRosterBundle(characters, items, petBagSlots, costumeSlots);
    }

    public async ValueTask<int> CreateAsync(
        int accountId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short mapId,
        float posX,
        float posY,
        float posZ,
        int life,
        int maxLife,
        int mana,
        int maxMana,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Create", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("Name", name, SqlDbType.NVarChar)
            .AddParameter("Tribe", tribe, SqlDbType.TinyInt)
            .AddParameter("Gender", gender, SqlDbType.TinyInt)
            .AddParameter("HeadType", headType, SqlDbType.TinyInt)
            .AddParameter("FaceType", faceType, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .AddParameter("PosX", posX, SqlDbType.Real)
            .AddParameter("PosY", posY, SqlDbType.Real)
            .AddParameter("PosZ", posZ, SqlDbType.Real)
            .AddParameter("Life", life, SqlDbType.Int)
            .AddParameter("MaxLife", maxLife, SqlDbType.Int)
            .AddParameter("Mana", mana, SqlDbType.Int)
            .AddParameter("MaxMana", maxMana, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> CreateWithStarterKitAsync(
        int accountId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short mapId,
        float posX,
        float posY,
        float posZ,
        int life,
        int maxLife,
        int mana,
        int maxMana,
        int welcomeBuffUntilDate,
        long premiumUntilUnixSeconds,
        IReadOnlyList<CharacterItemSlotTvp> equipment,
        IReadOnlyList<CharacterItemSlotTvp> inventory,
        IReadOnlyList<CharacterSkillSlotTvp> skills,
        IReadOnlyList<CharacterHotkeySlotTvp> hotkeys,
        CancellationToken ct,
        byte previousTribe = 0)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_CreateWithStarterKit", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("Name", name, SqlDbType.NVarChar)
            .AddParameter("Tribe", tribe, SqlDbType.TinyInt)
            .AddParameter("Gender", gender, SqlDbType.TinyInt)
            .AddParameter("HeadType", headType, SqlDbType.TinyInt)
            .AddParameter("FaceType", faceType, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .AddParameter("PosX", posX, SqlDbType.Real)
            .AddParameter("PosY", posY, SqlDbType.Real)
            .AddParameter("PosZ", posZ, SqlDbType.Real)
            .AddParameter("Life", life, SqlDbType.Int)
            .AddParameter("MaxLife", maxLife, SqlDbType.Int)
            .AddParameter("Mana", mana, SqlDbType.Int)
            .AddParameter("MaxMana", maxMana, SqlDbType.Int)
            .AddParameter("WelcomeBuffUntilDate", welcomeBuffUntilDate, SqlDbType.Int)
            .AddParameter("PremiumUntilUnixSeconds", premiumUntilUnixSeconds, SqlDbType.BigInt);

        if (equipment.Count > 0)
            builder.AddTvpParameter("Equipment", equipment);

        if (inventory.Count > 0)
            builder.AddTvpParameter("Inventory", inventory);

        if (skills.Count > 0)
            builder.AddTvpParameter("Skills", skills);

        if (hotkeys.Count > 0)
            builder.AddTvpParameter("Hotkeys", hotkeys);

        builder.AddParameter("PreviousTribe", previousTribe, SqlDbType.TinyInt);

        return await Db.ExecuteScalarAsync<int>(builder.Build(), ct);
    }

    public async ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Delete", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntrySummary", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterWorldEntryDto>(sp, ct);
    }

    public async ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistBatch", 0)
            .AddTvpParameter("Positions", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ClampVitalsFloorAsync(int characterId, long flushSequence, int life, int mana,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_ClampVitalsFloor", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("FlushSequence", flushSequence, SqlDbType.BigInt)
            .AddParameter("Life", life, SqlDbType.Int)
            .AddParameter("Mana", mana, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntry", 64)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var (characters, items, skills, hotkeys, buffs) = await Db
            .QueryMultipleReadOnlyCollectionAsync<CharacterWorldSnapshotDto, CharacterItemSlotDto, CharacterSkillDto,
                CharacterHotkeyDto, CharacterBuffDto>(sp, ct);

        return characters.Count == 0
            ? null
            : new CharacterWorldEntryBundle(characters[0], items, skills, hotkeys, buffs);
    }

    public async ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterItems_ReplaceContainer", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterItems_ReplaceTwoContainers", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ContainerA", containerA, SqlDbType.TinyInt);

        if (itemsA.Count > 0)
            builder.AddTvpParameter("ItemsA", itemsA);

        builder.AddParameter("ContainerB", containerB, SqlDbType.TinyInt);

        if (itemsB.Count > 0)
            builder.AddTvpParameter("ItemsB", itemsB);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ReplaceContainerV2Async(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotV2Tvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterItems_ReplaceContainerV2", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ReplaceTwoContainersV2Async(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotV2Tvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotV2Tvp> itemsB,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterItems_ReplaceTwoContainersV2", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ContainerA", containerA, SqlDbType.TinyInt);

        if (itemsA.Count > 0)
            builder.AddTvpParameter("ItemsA", itemsA);

        builder.AddParameter("ContainerB", containerB, SqlDbType.TinyInt);

        if (itemsB.Count > 0)
            builder.AddTvpParameter("ItemsB", itemsB);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask<ReadOnlyCollection<CharacterCostumeSlotDto>> GetCostumesAsync(int characterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetCostumes", 10)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterCostumeSlotDto>(sp, ct);
    }

    public async ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows,
        IReadOnlyList<CharacterCostumeSlotTvp> costumes, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_PersistProgressBatch", 0)
            .AddTvpParameter("Progress", rows);

        if (costumes.Count > 0)
            builder.AddTvpParameter("Costumes", costumes);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask PersistFinalFlushAsync(CharacterProgressTvp progress, CharacterPositionTvp position,
        IReadOnlyList<CharacterCostumeSlotTvp> costumes, CancellationToken ct)
    {
        IReadOnlyList<CharacterProgressTvp> progressRows = [progress];
        IReadOnlyList<CharacterPositionTvp> positionRows = [position];

        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_PersistFinalFlush", 0)
            .AddTvpParameter("Progress", progressRows)
            .AddTvpParameter("Position", positionRows);

        if (costumes.Count > 0)
            builder.AddTvpParameter("Costumes", costumes);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustStoreMoneyAsync(int characterId, long deltaMoney, long deltaStoreMoney,
        CancellationToken ct, int? auditAccountId = null, short? auditEventCode = null, int? auditQuantity = null)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustStoreMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaStoreMoney", deltaStoreMoney, SqlDbType.BigInt)
            .AddParameter("AuditAccountId", (object?)auditAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("AuditEventCode", (object?)auditEventCode ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("AuditQuantity", (object?)auditQuantity ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int? auditAccountId = null, short? auditEventCode = null, int? auditItemId = null,
        int? auditQuantity = null, string? auditPayload = null)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoneyAndReplaceContainer", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        builder.AddParameter("AuditAccountId", (object?)auditAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("AuditEventCode", (object?)auditEventCode ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("AuditItemId", (object?)auditItemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("AuditQuantity", (object?)auditQuantity ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("AuditPayload", (object?)auditPayload ?? DBNull.Value, SqlDbType.NVarChar);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask AdjustMoneyAndReplaceTwoContainersAsync(int characterId, long deltaMoney,
        int deltaBigMoney, byte containerA, IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoneyAndReplaceTwoContainers",
                0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("ContainerA", containerA, SqlDbType.TinyInt);

        if (itemsA.Count > 0)
            builder.AddTvpParameter("ItemsA", itemsA);

        builder.AddParameter("ContainerB", containerB, SqlDbType.TinyInt);

        if (itemsB.Count > 0)
            builder.AddTvpParameter("ItemsB", itemsB);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask UpsertSkillSlotAsync(int characterId, byte slotIndex, int skillId, int grade,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterSkills_UpsertSlot", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("SkillId", skillId, SqlDbType.Int)
            .AddParameter("Grade", grade, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask UpsertHotkeySlotAsync(int characterId, byte page, byte keyIndex, int sort, int value1,
        int value2, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterHotkeys_UpsertSlot", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Page", page, SqlDbType.TinyInt)
            .AddParameter("KeyIndex", keyIndex, SqlDbType.TinyInt)
            .AddParameter("Sort", sort, SqlDbType.Int)
            .AddParameter("Value1", value1, SqlDbType.Int)
            .AddParameter("Value2", value2, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ExecuteTradeAsync(
        int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0, IReadOnlyList<CharacterItemSlotTvp> itemsA1,
        long deltaMoneyA, int deltaBigMoneyA,
        int characterB, IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB,
        CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterTrade_Execute", 0)
            .AddParameter("CharacterA", characterA, SqlDbType.Int);

        if (itemsA0.Count > 0) builder.AddTvpParameter("ItemsA0", itemsA0);
        if (itemsA1.Count > 0) builder.AddTvpParameter("ItemsA1", itemsA1);

        builder.AddParameter("DeltaMoneyA", deltaMoneyA, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoneyA", deltaBigMoneyA, SqlDbType.Int)
            .AddParameter("CharacterB", characterB, SqlDbType.Int);

        if (itemsB0.Count > 0) builder.AddTvpParameter("ItemsB0", itemsB0);
        if (itemsB1.Count > 0) builder.AddTvpParameter("ItemsB1", itemsB1);

        builder.AddParameter("DeltaMoneyB", deltaMoneyB, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoneyB", deltaBigMoneyB, SqlDbType.Int);

        if (tradedItemsA is { Count: > 0 }) builder.AddTvpParameter("TradedItemsA", tradedItemsA);
        if (tradedItemsB is { Count: > 0 }) builder.AddTvpParameter("TradedItemsB", tradedItemsB);

        builder.AddParameter("OfferedMoneyA", offeredMoneyA, SqlDbType.BigInt)
            .AddParameter("OfferedBigMoneyA", offeredBigMoneyA, SqlDbType.Int)
            .AddParameter("OfferedMoneyB", offeredMoneyB, SqlDbType.BigInt)
            .AddParameter("OfferedBigMoneyB", offeredBigMoneyB, SqlDbType.Int);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId,
        int qSort, int targetPhase, int killCounter, long deltaMoney,
        byte? container1, IReadOnlyList<CharacterItemSlotTvp> items1,
        byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterQuest_ApplyTransition", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("StepPermanent", stepPermanent, SqlDbType.Int)
            .AddParameter("ActiveQuestId", activeQuestId, SqlDbType.Int)
            .AddParameter("QSort", qSort, SqlDbType.Int)
            .AddParameter("TargetPhase", targetPhase, SqlDbType.Int)
            .AddParameter("KillCounter", killCounter, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("Container1", container1 ?? NoContainer, SqlDbType.TinyInt);

        if (container1 is not null && items1.Count > 0)
            builder.AddTvpParameter("Items1", items1);

        builder.AddParameter("Container2", container2 ?? NoContainer, SqlDbType.TinyInt);

        if (container2 is not null && items2.Count > 0)
            builder.AddTvpParameter("Items2", items2);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ApplyDailyMissionClaimAsync(int characterId, int joinWar, int killOtherTribe,
        int killMonster, int playTime, byte? container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_ApplyDailyMissionClaim", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("JoinWar", joinWar, SqlDbType.Int)
            .AddParameter("KillOtherTribe", killOtherTribe, SqlDbType.Int)
            .AddParameter("KillMonster", killMonster, SqlDbType.Int)
            .AddParameter("PlayTime", playTime, SqlDbType.Int)
            .AddParameter("Container", container ?? NoContainer, SqlDbType.TinyInt);

        if (container is not null && items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask SetAutoPotionThresholdAsync(int characterId, byte autoLifeRatio, byte autoManaRatio,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_SetAutoPotionThreshold", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("AutoLifeRatio", autoLifeRatio, SqlDbType.TinyInt)
            .AddParameter("AutoManaRatio", autoManaRatio, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_SetAutoHunt", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Enabled", enabled, SqlDbType.Bit)
            .AddParameter("Config", config, SqlDbType.VarBinary)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetIdByName", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar)
            .Build();

        var row = await Db.FirstQueryAsync<CharacterIdDto>(sp, ct);
        return row?.CharacterId;
    }

    public async ValueTask<int?> GetItemIdAtSlotAsync(int characterId, byte container, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterItem_GetIdAtSlot", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        var row = await Db.FirstQueryAsync<CharacterItemIdDto>(sp, ct);
        return row?.ItemId;
    }

    public async ValueTask<RewardClaimStateDto?> GetAccountRewardClaimStateAsync(int accountId, int todayDate,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_GetState", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("TodayDate", todayDate, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<RewardClaimStateDto>(sp, ct);
    }

    public async ValueTask ClaimAccountDailyRewardAsync(int accountId, int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_Claim", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("TodayDate", todayDate, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask<int> SpendBloodCoinAndReplaceContainerAsync(int characterId, int deltaBloodCoin,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_SpendBloodCoinAndReplaceContainer", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBloodCoin", deltaBloodCoin, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        return await Db.ExecuteScalarAsync<int>(builder.Build(), ct);
    }

    public async ValueTask ExecutePshopPurchaseAsync(int sellerCharacterId, byte sellerContainer,
        IReadOnlyList<CharacterItemSlotTvp> sellerItems, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, int price, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_PshopPurchase_Execute", 0)
            .AddParameter("SellerCharacterId", sellerCharacterId, SqlDbType.Int)
            .AddParameter("SellerContainer", sellerContainer, SqlDbType.TinyInt);

        if (sellerItems.Count > 0) builder.AddTvpParameter("SellerItems", sellerItems);

        builder.AddParameter("BuyerCharacterId", buyerCharacterId, SqlDbType.Int)
            .AddParameter("BuyerContainer", buyerContainer, SqlDbType.TinyInt);

        if (buyerItems.Count > 0) builder.AddTvpParameter("BuyerItems", buyerItems);

        builder.AddParameter("Price", price, SqlDbType.Int);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GrantTribeTransferPermit", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> AdjustDeathProtectionAsync(int characterId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustDeathProtection", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> AdjustZone241TimeAsync(int characterId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustZone241Time", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask ApplyTribeConversionAsync(int characterId, int itemId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_ApplyTribeConversion", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ItemId", itemId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ApplyTribeFourConversionAsync(int characterId, byte newTribe, int stepPermanent,
        int activeQuestId, int qSort, int targetPhase, int killCounter, bool consumeSharedQuota,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_ApplyTribeFourConversion", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("NewTribe", newTribe, SqlDbType.TinyInt)
            .AddParameter("StepPermanent", stepPermanent, SqlDbType.Int)
            .AddParameter("ActiveQuestId", activeQuestId, SqlDbType.Int)
            .AddParameter("QSort", qSort, SqlDbType.Int)
            .AddParameter("TargetPhase", targetPhase, SqlDbType.Int)
            .AddParameter("KillCounter", killCounter, SqlDbType.Int)
            .AddParameter("ConsumeQuota", consumeSharedQuota, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
