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
    /// <summary>
    ///     Sentinel for ApplyQuestTransitionAsync/ApplyDailyMissionClaimAsync's @ContainerN; no valid container id is
    ///     255.
    /// </summary>
    public const byte NoContainer = 255;

    /// <summary>Character-select list for the account. Capacity 3 = MAX_USER_AVATAR_NUM, the legacy 3-slot cap.</summary>
    public async ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetByAccount", 3)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterSummaryDto>(sp, ct);
    }

    /// <summary>Creates a character in the given slot; returns the new CharacterId (usp_Character_Create's scalar result).</summary>
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

    /// <summary>
    ///     Op17's full creation path -- usp_Character_CreateWithStarterKit's scalar CharacterId result. Empty-TVP
    ///     omission follows the same rule as <see cref="ReplaceContainerAsync" />.
    /// </summary>
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
        CancellationToken ct)
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

        return await Db.ExecuteScalarAsync<int>(builder.Build(), ct);
    }

    /// <summary>Deletes the character occupying (AccountId, Slot) -- CL_DELETE_AVATAR_SEND's target.</summary>
    public async ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Delete", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Full world-entry snapshot for ZC_REGISTER_AVATAR_RECV/AVATAR_INFO; null if the character vanished mid-flight.</summary>
    public async ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntry", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterWorldEntryDto>(sp, ct);
    }

    /// <summary>
    ///     Write-behind position flush; usp_Character_PersistBatch is idempotent on FlushSequence, so a retry never
    ///     regresses a position.
    /// </summary>
    public async ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return; // SQL Server rejects an empty TVP outright -- never build the call for nothing to flush

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistBatch", 0)
            .AddTvpParameter("Positions", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     All 5 result sets of usp_Character_GetForWorldEntry in one round trip; null if the character vanished
    ///     mid-flight. <see cref="GetForWorldEntryAsync" /> stays the cheap prefix read -- this is the full snapshot.
    /// </summary>
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

    /// <summary>
    ///     Transactional DELETE+INSERT replace of one container (not write-behind). Empty list = deliberate clear -- the
    ///     TVP param is omitted since ADO.NET rejects a zero-row TVP.
    /// </summary>
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

    /// <summary>
    ///     Replaces TWO containers in one transaction -- e.g. equip (inventory -&gt; equipment). Calling
    ///     <see cref="ReplaceContainerAsync" /> twice could durably remove an item from one container without adding it to the
    ///     other; this closes that window.
    /// </summary>
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

    /// <summary>
    ///     Write-behind progression flush, idempotent on the same per-character FlushSequence as
    ///     <see cref="PersistPositionsAsync" /> -- replays never regress state.
    /// </summary>
    public async ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return; // SQL Server rejects an empty TVP outright -- never build the call for nothing to flush

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistProgressBatch", 0)
            .AddTvpParameter("Progress", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Atomic money adjustment; throws SQL 50222 instead of clamping when either pool would go negative.</summary>
    public async ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Atomic money adjustment + one container replace, e.g. NPC-shop buy/sell -- a mid-sequence failure must never
    ///     pay without granting the item (or vice versa). Same empty-TVP-omission rule as <see cref="ReplaceContainerAsync" />
    ///     .
    /// </summary>
    public async ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoneyAndReplaceContainer", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    /// <summary>
    ///     Atomic money adjustment + two container replaces -- e.g. an enchant whose target and material slots land on
    ///     different inventory pages.
    /// </summary>
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

    /// <summary>
    ///     Durable single-slot write to game.CharacterSkills; SkillPoints itself is deliberately not touched here. Covers
    ///     both learn (tSort 202/233) and upgrade (tSort 203) -- both are just this slot's final (SkillId, Grade).
    /// </summary>
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

    /// <summary>
    ///     Atomic two-character trade commit -- both sides' final inventory contents and money deltas commit in one
    ///     transaction or none do. TradeSession has already computed every value; this is the durable commit, not the
    ///     negotiation.
    /// </summary>
    public async ValueTask ExecuteTradeAsync(
        int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0, IReadOnlyList<CharacterItemSlotTvp> itemsA1,
        long deltaMoneyA, int deltaBigMoneyA,
        int characterB, IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB,
        CancellationToken ct)
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

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    /// <summary>
    ///     Atomically upserts quest state plus optional money credit and up to two container replaces, in one
    ///     transaction. Unlike every other money proc here, a money-cap breach is silently skipped, not thrown. A (container,
    ///     items) pair is ignored unless container is non-null; passing the same container twice is a caller error.
    /// </summary>
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

    /// <summary>
    ///     Atomically writes the 4 daily-mission counters (after deduction) plus an optional one-container reward
    ///     deposit.
    /// </summary>
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

    /// <summary>Persists CZ_CHANGE_AUTO_INFO's two auto-potion thresholds.</summary>
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

    /// <summary>
    ///     Persists the auto-hunt flag and the raw 112-byte AUTO_HUNT blob verbatim, no validation -- matches the legacy
    ///     CopyMemory.
    /// </summary>
    public async ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_SetAutoHunt", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Enabled", enabled, SqlDbType.Bit)
            .AddParameter("Config", config, SqlDbType.VarBinary)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Persists the active pet's growth/activity counters.</summary>
    public async ValueTask SetPetGrowthAsync(int characterId, int petGrowth, byte petActivity, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_SetPetGrowth", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PetGrowth", petGrowth, SqlDbType.Int)
            .AddParameter("PetActivity", petActivity, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Resolves an avatar name to its CharacterId regardless of online state -- used to view another (possibly
    ///     offline) character's shop stall.
    /// </summary>
    public async ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetIdByName", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar)
            .Build();

        var row = await Db.FirstQueryAsync<CharacterIdDto>(sp, ct);
        return row?.CharacterId;
    }

    /// <summary>
    ///     CZ_GET_REWARD_ITEM_SEND's read; null only if the character doesn't exist. todayDate (app-clock YYYYMMDD)
    ///     drives the proc's lazy weekly reset of RewardClaimDay.
    /// </summary>
    public async ValueTask<RewardClaimStateDto?> GetRewardClaimStateAsync(int characterId, int todayDate,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetRewardClaimState", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("TodayDate", todayDate, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<RewardClaimStateDto>(sp, ct);
    }

    /// <summary>
    ///     Atomically advances the 7-day login-reward cursor and grants the day's item. Throws SQL 50270 if already
    ///     claimed today, fully claimed, or unknown character.
    /// </summary>
    public async ValueTask ClaimDailyRewardAsync(int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_ClaimDailyReward", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("TodayDate", todayDate, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    /// <summary>
    ///     Atomic BloodCoin spend + one container replace (CZ_BUY_BLOOD_MARK_SEND). Returns the post-debit balance.
    ///     Throws SQL 50271 on insufficient balance.
    /// </summary>
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

    /// <summary>
    ///     Atomic two-character live personal-shop-stall purchase (CZ_BUY_PSHOP_SEND). No item-slot CAS guard needed --
    ///     caller already re-validated under both participants' EconomyActionLock.
    /// </summary>
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

    /// <summary>
    ///     game.Characters.TribeTransferPermitCount adjustment (Faction Transfer Scroll, world.Items 8153/8154).
    ///     Returns the post-adjustment balance. Throws SQL 50312 on unknown character or an adjustment that
    ///     would take the balance negative.
    /// </summary>
    public async ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GrantTribeTransferPermit", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
