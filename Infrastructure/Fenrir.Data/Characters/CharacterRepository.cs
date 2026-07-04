using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Commerce;

namespace Fenrir.Data.Characters;

/// <summary>
///     game.Characters access (architecture reference §11.1-§11.3). Singleton, injected only with
///     ICaeriusNetDbContext -- no SqlDbType or builder ever leaks past this type; callers see typed ValueTasks only.
/// </summary>
public sealed record CharacterRepository(ICaeriusNetDbContext Db) : ICharacterRepository
{
    /// <summary>
    ///     Sentinel for <see cref="ApplyQuestTransitionAsync" />/<see cref="ApplyDailyMissionClaimAsync" />'s
    ///     <c>@ContainerN</c> -- no valid container id ever uses 255.
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

    /// <summary>Deletes the character occupying (AccountId, Slot) -- CL_DELETE_AVATAR_SEND's target (wire contract §4.4).</summary>
    public async ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Delete", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Full world-entry snapshot for the ZC_REGISTER_AVATAR_RECV/AVATAR_INFO path (wire contract §6.2); null if the
    ///     character vanished mid-flight.
    /// </summary>
    public async ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntry", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterWorldEntryDto>(sp, ct);
    }

    /// <summary>
    ///     Write-behind position flush (architecture reference §10.5/§11.3); usp_Character_PersistBatch is idempotent on
    ///     FlushSequence, so a network retry never regresses a position.
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
    ///     Everything world entry needs in ONE round trip: the five result sets of the A3-extended
    ///     usp_Character_GetForWorldEntry (character+progression+quest state, items, skills, hotkeys, buffs).
    ///     Null if the character vanished mid-flight (empty RS0). <see cref="GetForWorldEntryAsync" /> stays as the
    ///     cheap M1-prefix read; this is the full snapshot the AVATAR_INFO/PlayerRuntimeState build consumes.
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
    ///     Whole-container replace of one character's item slots (usp_CharacterItems_ReplaceContainer, transactional
    ///     DELETE+INSERT -- D7 regime (b): item state never rides the lossy write-behind path). An EMPTY list is a
    ///     legal, deliberate "clear the container": the TVP parameter is simply omitted (a READONLY TVP defaults to an
    ///     empty table server-side), because ADO.NET rejects streaming a zero-row TVP outright.
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
    ///     Whole-container replace of TWO containers in ONE transaction (usp_CharacterItems_ReplaceTwoContainers,
    ///     D7 regime (b)) -- the cross-container twin of <see cref="ReplaceContainerAsync" />, for a single client
    ///     move whose FROM and TO slots live in different containers (e.g. equip: inventory -&gt; equipment).
    ///     Calling <see cref="ReplaceContainerAsync" /> twice for such a move would commit each container in its
    ///     OWN transaction -- a fault between the two calls could durably remove an item from its source without
    ///     ever durably adding it to its destination. This method closes that window: both containers commit or
    ///     roll back together.
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
    ///     Write-behind progression flush (D7 regime (a)) -- the progression twin of
    ///     <see cref="PersistPositionsAsync" />, idempotent on the same per-character FlushSequence, so replays of
    ///     either batch flavor never regress state.
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

    /// <summary>
    ///     Atomic money adjustment with an overdraft guard (usp_Character_AdjustMoney, D7 regime (b)): throws
    ///     SQL error 50222 instead of clamping when either pool would go negative -- a caller relying on "the debit
    ///     happened" without checking must never silently under-pay.
    /// </summary>
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
    ///     Atomic money adjustment + ONE container replace (usp_Character_AdjustMoneyAndReplaceContainer, D7
    ///     regime (b)) -- the single-character, one-container twin of <see cref="ReplaceTwoContainersAsync" />,
    ///     for an NPC-shop buy/sell (V5 NPC &amp; Economy): a mid-sequence failure must never let a character
    ///     pay without receiving the item (or vice versa). Same empty-TVP-omission rule as
    ///     <see cref="ReplaceContainerAsync" />.
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
    ///     Atomic money adjustment + TWO container replaces (usp_Character_AdjustMoneyAndReplaceTwoContainers,
    ///     D7 regime (b)) -- used when an IMPROVE_ITEM (enchant) attempt's target and material slots land on
    ///     DIFFERENT inventory pages. See <see cref="AdjustMoneyAndReplaceContainerAsync" />'s own remarks.
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
    ///     Durable single-slot write to game.CharacterSkills (usp_CharacterSkills_UpsertSlot, D7 regime (b) --
    ///     see that proc's own header comment for why SkillPoints itself is deliberately NOT touched here).
    ///     Covers both "learn a new skill" (tSort 202/233) and "upgrade an already-learned skill" (tSort 203):
    ///     both are just this slot's final (SkillId, Grade).
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
    ///     The atomic two-character trade commit (usp_CharacterTrade_Execute, Phase C/V6 Social -- D7
    ///     regime (b), extended past <see cref="ReplaceTwoContainersAsync" />'s one-character shape):
    ///     both sides' FINAL InventoryPage0/InventoryPage1 contents and both sides' money deltas commit
    ///     in ONE transaction, or none of them do. The sole caller (<c>TradeSession</c>,
    ///     Fenrir.Application.Game.Social.Trade) has already computed every projected container and delta
    ///     before calling this -- this method is the durable commit, not the negotiation.
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
    ///     Server Logic V9 Progression: atomically upserts the quest-state row (game.CharacterQuests) plus
    ///     OPTIONAL money credit and OPTIONAL up-to-TWO-container item replace, in ONE transaction
    ///     (usp_CharacterQuest_ApplyTransition -- see that proc's own header for why a money-cap breach is
    ///     SILENTLY skipped here rather than thrown, unlike every other money proc in this repository, and
    ///     for why TWO containers: a quest Complete's reward-item deposit and its quest-item deletion can
    ///     legitimately land on different inventory pages). Each (container, items) pair is ignored unless
    ///     its container is non-null; passing the SAME container twice is a caller error (merge the two
    ///     edits into one dictionary/one call site first).
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
    ///     Server Logic V9 Progression: atomically writes the 4 daily-mission counters (AFTER deduction)
    ///     plus an OPTIONAL one-container reward-item deposit (usp_Character_ApplyDailyMissionClaim).
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

    /// <summary>Server Logic V9 Progression: silent persistence of CZ_CHANGE_AUTO_INFO's two auto-potion thresholds.</summary>
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
    ///     Server Logic V9 Progression: persists the auto-hunt on/off flag and the raw 112-byte AUTO_HUNT
    ///     blob verbatim (usp_Character_SetAutoHunt) -- no content validation, matching the verified legacy
    ///     <c>CopyMemory</c>.
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

    /// <summary>Server Logic V9 Progression: persists the active pet's growth/activity counters (usp_Character_SetPetGrowth).</summary>
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
    ///     Resolves an avatar NAME to its CharacterId regardless of online state (usp_Character_GetIdByName) -- V8's own
    ///     need: an offline-shop "view another character's stall" lookup where the target need not be online.
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
    ///     CZ_GET_REWARD_ITEM_SEND's own read (usp_Character_GetRewardClaimState) -- null only if the
    ///     character does not exist. <paramref name="todayDate" /> (caller's app-clock YYYYMMDD, same
    ///     convention as <see cref="ClaimDailyRewardAsync" />) drives the proc's own lazy weekly reset of
    ///     the reported RewardClaimDay -- see that proc's header comment.
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
    ///     Atomically advances the 7-day login-reward cursor and grants the day's item (usp_Character_ClaimDailyReward,
    ///     D7 regime (b)). Throws SQL 50270 if already claimed today / fully claimed / unknown character.
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
    ///     Atomic BloodCoin spend + ONE container replace (usp_Character_SpendBloodCoinAndReplaceContainer,
    ///     CZ_BUY_BLOOD_MARK_SEND, D7 regime (b)). Returns the post-debit balance. Throws SQL 50271 on an
    ///     insufficient balance.
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
    ///     The atomic two-character LIVE personal-shop-stall purchase commit (usp_PshopPurchase_Execute,
    ///     CZ_BUY_PSHOP_SEND, D7 regime (b)) -- see that proc's own header for why no item-slot CAS guard is
    ///     needed here (the caller already re-validated under both participants' EconomyActionLock).
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
}
