using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Data.Abstractions.Characters;

public interface ICharacterRepository
{
    public ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId, CancellationToken ct);

    public ValueTask<CharacterAccountRosterBundle> GetAccountRosterAsync(int accountId, CancellationToken ct);

    public ValueTask<int> CreateAsync(
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
        CancellationToken ct);

    public ValueTask<int> CreateWithStarterKitAsync(
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
        byte previousTribe = 0);

    public ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct);

    public ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct);

    public ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct);

    public ValueTask ClampVitalsFloorAsync(int characterId, long flushSequence, int life, int mana,
        CancellationToken ct);

    public ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct);

    public ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct);

    public ValueTask ReplaceContainerV2Async(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotV2Tvp> items, CancellationToken ct);

    public ValueTask ReplaceTwoContainersV2Async(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotV2Tvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotV2Tvp> itemsB,
        CancellationToken ct);

    public ValueTask<ReadOnlyCollection<CharacterCostumeSlotDto>> GetCostumesAsync(int characterId,
        CancellationToken ct);

    public ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows,
        IReadOnlyList<CharacterCostumeSlotTvp> costumes, CancellationToken ct);

    public ValueTask PersistFinalFlushAsync(CharacterProgressTvp progress, CharacterPositionTvp position,
        IReadOnlyList<CharacterCostumeSlotTvp> costumes, CancellationToken ct);

    public ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct);

    public ValueTask AdjustStoreMoneyAsync(int characterId, long deltaMoney, long deltaStoreMoney,
        CancellationToken ct, int? auditAccountId = null, short? auditEventCode = null, int? auditQuantity = null);

    public ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int? auditAccountId = null, short? auditEventCode = null, int? auditItemId = null,
        int? auditQuantity = null, string? auditPayload = null);

    public ValueTask AdjustMoneyAndReplaceTwoContainersAsync(int characterId, long deltaMoney,
        int deltaBigMoney, byte containerA, IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB, CancellationToken ct);

    public ValueTask UpsertSkillSlotAsync(int characterId, byte slotIndex, int skillId, int grade,
        CancellationToken ct);

    public ValueTask UpsertHotkeySlotAsync(int characterId, byte page, byte keyIndex, int sort, int value1,
        int value2, CancellationToken ct);

    public ValueTask ExecuteTradeAsync(
        int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0, IReadOnlyList<CharacterItemSlotTvp> itemsA1,
        long deltaMoneyA, int deltaBigMoneyA,
        int characterB, IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB,
        CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0);

    public ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId,
        int qSort, int targetPhase, int killCounter, long deltaMoney,
        byte? container1, IReadOnlyList<CharacterItemSlotTvp> items1,
        byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct);

    public ValueTask ApplyDailyMissionClaimAsync(int characterId, int joinWar, int killOtherTribe,
        int killMonster, int playTime, byte? container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct);

    public ValueTask SetAutoPotionThresholdAsync(int characterId, byte autoLifeRatio, byte autoManaRatio,
        CancellationToken ct);

    public ValueTask UpdateAppearanceAsync(int characterId, byte headType, byte faceType, CancellationToken ct);

        public ValueTask UpdateGenderAndAppearanceAsync(int characterId, byte gender, byte headType, byte faceType,
        CancellationToken ct);

    public ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct);

    public ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct);

    public ValueTask<int?> GetItemIdAtSlotAsync(int characterId, byte container, byte slot, CancellationToken ct);

    public ValueTask<RewardClaimStateDto?> GetAccountRewardClaimStateAsync(int accountId, int todayDate,
        CancellationToken ct);

    public ValueTask ClaimAccountDailyRewardAsync(int accountId, int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask<int> SpendBloodCoinAndReplaceContainerAsync(int characterId, int deltaBloodCoin,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask ExecutePshopPurchaseAsync(int sellerCharacterId, byte sellerContainer,
        IReadOnlyList<CharacterItemSlotTvp> sellerItems, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, int price, CancellationToken ct);

    public ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct);

    public ValueTask<int> AdjustDeathProtectionAsync(int characterId, int delta, CancellationToken ct);

    public ValueTask<int> AdjustZone241TimeAsync(int characterId, int delta, CancellationToken ct);

    public ValueTask ApplyTribeConversionAsync(int characterId, int itemId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    public ValueTask ApplyTribeFourConversionAsync(int characterId, byte newTribe, int stepPermanent,
        int activeQuestId, int qSort, int targetPhase, int killCounter, bool consumeSharedQuota,
        CancellationToken ct);
}
