using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeCharacterRepository : ICharacterRepository
{
    private readonly Dictionary<(int CharacterId, byte Container, byte Slot), int> _itemIdBySlot = new();

    private readonly List<CharacterRosterItemDto> _rosterItems = [];

    private readonly Dictionary<int, CharacterRosterDto> _rosterOverridesByCharacterId = new();

    private readonly List<CharacterSummaryDto> _summaries;
    private readonly Dictionary<int, CharacterWorldEntryDto> _worldEntriesByCharacterId;

    private int _nextCharacterId = 1000;

    private FakeCharacterRepository(IEnumerable<CharacterSummaryDto> summaries,
        IEnumerable<CharacterWorldEntryDto> worldEntries)
    {
        _summaries = [.. summaries];
        _worldEntriesByCharacterId = worldEntries.ToDictionary(w => w.CharacterId);
    }

    public Exception? CreateWithStarterKitException { get; set; }

    public Exception? DeleteException { get; set; }

    public CreateWithStarterKitCall? LastCreateWithStarterKit { get; private set; }

    public List<(int CharacterId, byte Container, byte Slot)> QueriedItemSlots { get; } = [];

    public List<(int AccountId, byte Slot)> DeleteCalls { get; } = [];

    public ClampVitalsFloorCall? LastClampVitalsFloor { get; private set; }

    public ValueTask<int?> GetItemIdAtSlotAsync(int characterId, byte container, byte slot, CancellationToken ct)
    {
        QueriedItemSlots.Add((characterId, container, slot));
        return ValueTask.FromResult<int?>(_itemIdBySlot.TryGetValue((characterId, container, slot), out var itemId)
            ? itemId
            : null);
    }

    public ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<CharacterSummaryDto>(_summaries));
    }

    public ValueTask<CharacterAccountRosterBundle> GetAccountRosterAsync(int accountId, CancellationToken ct)
    {
        var rosterCharacters = _summaries
            .Select(s => _rosterOverridesByCharacterId.GetValueOrDefault(s.CharacterId) ?? ToRosterDto(s))
            .ToList();

        return ValueTask.FromResult(new CharacterAccountRosterBundle(
            new ReadOnlyCollection<CharacterRosterDto>(rosterCharacters),
            new ReadOnlyCollection<CharacterRosterItemDto>(_rosterItems)));
    }

    public ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(_worldEntriesByCharacterId.GetValueOrDefault(characterId));
    }

    public ValueTask<int> CreateAsync(int accountId, byte slot, string name, byte tribe, byte gender,
        byte headType, byte faceType, short mapId, float posX, float posY, float posZ, int life, int maxLife,
        int mana, int maxMana, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> CreateWithStarterKitAsync(int accountId, byte slot, string name, byte tribe, byte gender,
        byte headType, byte faceType, short mapId, float posX, float posY, float posZ, int life, int maxLife,
        int mana, int maxMana, int welcomeBuffUntilDate, long premiumUntilUnixSeconds,
        IReadOnlyList<CharacterItemSlotTvp> equipment, IReadOnlyList<CharacterItemSlotTvp> inventory,
        IReadOnlyList<CharacterSkillSlotTvp> skills, IReadOnlyList<CharacterHotkeySlotTvp> hotkeys,
        CancellationToken ct, byte previousTribe = 0)
    {
        LastCreateWithStarterKit = new CreateWithStarterKitCall(accountId, slot, name, tribe, gender, headType,
            faceType, mapId, posX, posY, posZ, life, maxLife, mana, maxMana, welcomeBuffUntilDate,
            premiumUntilUnixSeconds, equipment, inventory, skills, hotkeys, previousTribe);

        if (CreateWithStarterKitException is { } exception)
            throw exception;

        var characterId = _nextCharacterId++;
        _worldEntriesByCharacterId[characterId] = new CharacterWorldEntryDto(
            characterId, accountId, slot, name, tribe, gender, headType, faceType,
            1, mapId, posX, posY, posZ, 0f, life, maxLife, mana, maxMana, 0L);

        return ValueTask.FromResult(characterId);
    }

    public ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        DeleteCalls.Add((accountId, slot));

        if (DeleteException is { } exception)
            throw exception;

        _summaries.RemoveAll(s => s.Slot == slot);
        return ValueTask.CompletedTask;
    }

    public ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClampVitalsFloorAsync(int characterId, long flushSequence, int life, int mana,
        CancellationToken ct)
    {
        LastClampVitalsFloor = new ClampVitalsFloorCall(characterId, flushSequence, life, mana);

        if (_worldEntriesByCharacterId.TryGetValue(characterId, out var existing))
            _worldEntriesByCharacterId[characterId] = existing with { Life = life, Mana = mana };

        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask PersistFinalFlushAsync(CharacterProgressTvp progress, CharacterPositionTvp position,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask AdjustStoreMoneyAsync(int characterId, long deltaMoney, long deltaStoreMoney,
        CancellationToken ct, int? auditAccountId = null, short? auditEventCode = null, int? auditQuantity = null)
    {
        throw new NotSupportedException();
    }

    public ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int? auditAccountId = null, short? auditEventCode = null, int? auditItemId = null,
        int? auditQuantity = null, string? auditPayload = null)
    {
        throw new NotSupportedException();
    }

    public ValueTask AdjustMoneyAndReplaceTwoContainersAsync(int characterId, long deltaMoney,
        int deltaBigMoney, byte containerA, IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask UpsertSkillSlotAsync(int characterId, byte slotIndex, int skillId, int grade,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask UpsertHotkeySlotAsync(int characterId, byte page, byte keyIndex, int sort, int value1,
        int value2, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ExecuteTradeAsync(int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0,
        IReadOnlyList<CharacterItemSlotTvp> itemsA1, long deltaMoneyA, int deltaBigMoneyA, int characterB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB, CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> AdjustDeathProtectionAsync(int characterId, int delta, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> AdjustZone241TimeAsync(int characterId, int delta, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ApplyTribeConversionAsync(int characterId, int itemId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ApplyTribeFourConversionAsync(int characterId, byte newTribe, int stepPermanent,
        int activeQuestId, int qSort, int targetPhase, int killCounter, bool consumeSharedQuota,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId, int qSort,
        int targetPhase, int killCounter, long deltaMoney, byte? container1,
        IReadOnlyList<CharacterItemSlotTvp> items1, byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ApplyDailyMissionClaimAsync(int characterId, int joinWar, int killOtherTribe,
        int killMonster, int playTime, byte? container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetAutoPotionThresholdAsync(int characterId, byte autoLifeRatio, byte autoManaRatio,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetPetGrowthAsync(int characterId, int petGrowth, byte petActivity, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetMountProgressionAsync(int characterId, int mountItemId, int mountExpActivity,
        int mountPower, int mountSlotIndex, int mountTime, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<RewardClaimStateDto?> GetRewardClaimStateAsync(int characterId, int todayDate,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClaimDailyRewardAsync(int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> SpendBloodCoinAndReplaceContainerAsync(int characterId, int deltaBloodCoin,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ExecutePshopPurchaseAsync(int sellerCharacterId, byte sellerContainer,
        IReadOnlyList<CharacterItemSlotTvp> sellerItems, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, int price, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    private static CharacterRosterDto ToRosterDto(CharacterSummaryDto summary)
    {
        return new CharacterRosterDto(
            summary.CharacterId,
            summary.Slot,
            summary.Name,
            summary.Tribe,
            0,
            summary.Gender,
            summary.HeadType,
            summary.FaceType,
            summary.Level,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0f,
            0f,
            0f,
            0,
            0);
    }

    public FakeCharacterRepository WithRosterCharacter(CharacterRosterDto rosterCharacter)
    {
        _rosterOverridesByCharacterId[rosterCharacter.CharacterId] = rosterCharacter;
        return this;
    }

    public FakeCharacterRepository WithRosterItem(CharacterRosterItemDto item)
    {
        _rosterItems.Add(item);
        return this;
    }

    public FakeCharacterRepository WithItemAtSlot(int characterId, byte container, byte slot, int itemId)
    {
        _itemIdBySlot[(characterId, container, slot)] = itemId;
        return this;
    }

    public static FakeCharacterRepository With(CharacterSummaryDto summary, CharacterWorldEntryDto worldEntry)
    {
        return new FakeCharacterRepository([summary], [worldEntry]);
    }

    public static FakeCharacterRepository WithNone()
    {
        return new FakeCharacterRepository([], []);
    }

    public static FakeCharacterRepository WithSummaries(params CharacterSummaryDto[] summaries)
    {
        return new FakeCharacterRepository(summaries, []);
    }
}

internal sealed record ClampVitalsFloorCall(int CharacterId, long FlushSequence, int Life, int Mana);

internal sealed record CreateWithStarterKitCall(
    int AccountId,
    byte Slot,
    string Name,
    byte Tribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    int Life,
    int MaxLife,
    int Mana,
    int MaxMana,
    int WelcomeBuffUntilDate,
    long PremiumUntilUnixSeconds,
    IReadOnlyList<CharacterItemSlotTvp> Equipment,
    IReadOnlyList<CharacterItemSlotTvp> Inventory,
    IReadOnlyList<CharacterSkillSlotTvp> Skills,
    IReadOnlyList<CharacterHotkeySlotTvp> Hotkeys,
    byte PreviousTribe);
