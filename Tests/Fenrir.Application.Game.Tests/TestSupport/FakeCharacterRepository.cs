using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for ICharacterRepository: only the container-replace/adjust-money paths used by the handler
///     test suites are exercised here; every other member is out of scope.
/// </summary>
internal sealed class FakeCharacterRepository : ICharacterRepository
{
    public (int CharacterId, byte Container, IReadOnlyList<CharacterItemSlotTvp> Items)? LastReplacedContainer
    {
        get;
        private set;
    }

    public (int CharacterId, long DeltaMoney, byte Container, IReadOnlyList<CharacterItemSlotTvp> Items)?
        LastAdjustMoneyAndReplaceContainer { get; private set; }

    public (int CharacterId, long DeltaMoney, byte ContainerA, IReadOnlyList<CharacterItemSlotTvp> ItemsA,
        byte ContainerB, IReadOnlyList<CharacterItemSlotTvp> ItemsB)? LastAdjustMoneyAndReplaceTwoContainers
    {
        get;
        private set;
    }

    public bool ThrowOnReplaceContainer { get; set; }
    public bool ThrowOnAdjustMoney { get; set; }

    /// <summary>
    ///     Scripted return for <see cref="GetForWorldEntryAsync" /> -- null (the default) exercises the "character
    ///     vanished" fallback path some callers (e.g. ZoneHandshakeService) defend against.
    /// </summary>
    public CharacterWorldEntryDto? WorldEntryToReturn { get; set; }

    /// <summary>
    ///     Scripted return for <see cref="GetWorldEntryBundleAsync" /> -- null (the default) exercises the
    ///     "world-entry bundle fetch failed" abort path (e.g. EnterWorldService); set this to exercise a
    ///     happy-path world entry instead.
    /// </summary>
    public CharacterWorldEntryBundle? WorldEntryBundleToReturn { get; set; }

    /// <summary>Every row ever passed to <see cref="PersistPositionsAsync" />, across every call -- append-only.</summary>
    public List<CharacterPositionTvp> PersistedPositionRows { get; } = [];

    /// <summary>Every row ever passed to <see cref="PersistProgressAsync" />, across every call -- append-only.</summary>
    public List<CharacterProgressTvp> PersistedProgressRows { get; } = [];

    /// <summary>Every call ever made to <see cref="ApplyQuestTransitionAsync" />, most-recent last -- append-only.</summary>
    public List<(int CharacterId, int StepPermanent, int ActiveQuestId, int QSort, int TargetPhase, int KillCounter,
        long DeltaMoney, byte? Container1, IReadOnlyList<CharacterItemSlotTvp> Items1, byte? Container2,
        IReadOnlyList<CharacterItemSlotTvp> Items2)> QuestTransitions { get; } = [];

    public int TribeTransferPermitCount { get; private set; }

    public (int CharacterId, int Delta)? LastGrantTribeTransferPermit { get; private set; }

    /// <summary>
    ///     Last call ever made to <see cref="UpsertHotkeySlotAsync" /> -- (Sort, Value1, Value2) is the raw legacy
    ///     triple.
    /// </summary>
    public (int CharacterId, byte Page, byte KeyIndex, int Sort, int Value1, int Value2)? LastUpsertHotkeySlot
    {
        get;
        private set;
    }

    public (int CharacterId, long DeltaMoney, long DeltaStoreMoney)? LastAdjustStoreMoney { get; private set; }

    /// <summary>Last call ever made to <see cref="AdjustMoneyAsync" /> -- (CharacterId, DeltaMoney, DeltaBigMoney).</summary>
    public (int CharacterId, long DeltaMoney, int DeltaBigMoney)? LastAdjustMoney { get; private set; }

    public bool ThrowOnAdjustZone241Time { get; set; }

    public (int CharacterId, int Delta)? LastAdjustZone241Time { get; private set; }

    public int Zone241Time { get; private set; }

    /// <summary>Every call ever made to <see cref="ApplyTribeFourConversionAsync" />, most-recent last -- append-only.</summary>
    public List<(int CharacterId, byte NewTribe, int StepPermanent, int ActiveQuestId, int QSort, int TargetPhase,
        int KillCounter)> TribeFourConversions { get; } = [];

    public ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (ThrowOnReplaceContainer)
            throw new InvalidOperationException("Simulated SQL failure");

        LastReplacedContainer = (characterId, container, items);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<CharacterAccountRosterBundle> GetAccountRosterAsync(int accountId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> CreateAsync(int accountId, byte slot, string name, byte tribe, byte gender, byte headType,
        byte faceType, short mapId, float posX, float posY, float posZ, int life, int maxLife, int mana,
        int maxMana, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> CreateWithStarterKitAsync(int accountId, byte slot, string name, byte tribe, byte gender,
        byte headType, byte faceType, short mapId, float posX, float posY, float posZ, int life, int maxLife,
        int mana, int maxMana, int welcomeBuffUntilDate, long premiumUntilUnixSeconds,
        IReadOnlyList<CharacterItemSlotTvp> equipment, IReadOnlyList<CharacterItemSlotTvp> inventory,
        IReadOnlyList<CharacterSkillSlotTvp> skills, IReadOnlyList<CharacterHotkeySlotTvp> hotkeys,
        CancellationToken ct, byte previousTribe = 0)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(WorldEntryToReturn);
    }

    public ValueTask ClampVitalsFloorAsync(int characterId, long flushSequence, int life, int mana,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        PersistedPositionRows.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(WorldEntryBundleToReturn);
    }

    public ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct)
    {
        PersistedProgressRows.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        if (ThrowOnAdjustMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastAdjustMoney = (characterId, deltaMoney, deltaBigMoney);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustStoreMoneyAsync(int characterId, long deltaMoney, long deltaStoreMoney,
        CancellationToken ct)
    {
        if (ThrowOnAdjustMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastAdjustStoreMoney = (characterId, deltaMoney, deltaStoreMoney);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustMoneyAndReplaceContainerAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (ThrowOnAdjustMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastAdjustMoneyAndReplaceContainer = (characterId, deltaMoney, container, items);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustMoneyAndReplaceTwoContainersAsync(int characterId, long deltaMoney, int deltaBigMoney,
        byte containerA, IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB, CancellationToken ct)
    {
        if (ThrowOnAdjustMoney)
            throw new InvalidOperationException("Simulated SQL failure");

        LastAdjustMoneyAndReplaceTwoContainers = (characterId, deltaMoney, containerA, itemsA, containerB, itemsB);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpsertSkillSlotAsync(int characterId, byte slotIndex, int skillId, int grade,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpsertHotkeySlotAsync(int characterId, byte page, byte keyIndex, int sort, int value1,
        int value2, CancellationToken ct)
    {
        LastUpsertHotkeySlot = (characterId, page, keyIndex, sort, value1, value2);
        return ValueTask.CompletedTask;
    }

    public ValueTask ExecuteTradeAsync(int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0,
        IReadOnlyList<CharacterItemSlotTvp> itemsA1, long deltaMoneyA, int deltaBigMoneyA, int characterB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1, long deltaMoneyB,
        int deltaBigMoneyB, CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> AdjustDeathProtectionAsync(int characterId, int delta, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> AdjustZone241TimeAsync(int characterId, int delta, CancellationToken ct)
    {
        if (ThrowOnAdjustZone241Time)
            throw new InvalidOperationException("Simulated SQL failure");

        LastAdjustZone241Time = (characterId, delta);
        Zone241Time += delta;
        return ValueTask.FromResult(Zone241Time);
    }

    public ValueTask ApplyTribeConversionAsync(int characterId, int itemId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ApplyTribeFourConversionAsync(int characterId, byte newTribe, int stepPermanent,
        int activeQuestId, int qSort, int targetPhase, int killCounter, CancellationToken ct)
    {
        TribeFourConversions.Add((characterId, newTribe, stepPermanent, activeQuestId, qSort, targetPhase,
            killCounter));
        return ValueTask.CompletedTask;
    }

    public ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId, int qSort,
        int targetPhase, int killCounter, long deltaMoney, byte? container1,
        IReadOnlyList<CharacterItemSlotTvp> items1, byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct)
    {
        QuestTransitions.Add((characterId, stepPermanent, activeQuestId, qSort, targetPhase, killCounter, deltaMoney,
            container1, items1, container2, items2));
        return ValueTask.CompletedTask;
    }

    public ValueTask ApplyDailyMissionClaimAsync(int characterId, int joinWar, int killOtherTribe, int killMonster,
        int playTime, byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetAutoPotionThresholdAsync(int characterId, byte autoLifeRatio, byte autoManaRatio,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetAutoHuntAsync(int characterId, bool enabled, byte[] config, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPetGrowthAsync(int characterId, int petGrowth, byte petActivity, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int?> GetIdByNameAsync(string name, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int?> GetItemIdAtSlotAsync(int characterId, byte container, byte slot, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<RewardClaimStateDto?> GetRewardClaimStateAsync(int characterId, int todayDate,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ClaimDailyRewardAsync(int characterId, int todayDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> SpendBloodCoinAndReplaceContainerAsync(int characterId, int deltaBloodCoin, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ExecutePshopPurchaseAsync(int sellerCharacterId, byte sellerContainer,
        IReadOnlyList<CharacterItemSlotTvp> sellerItems, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, int price, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct)
    {
        LastGrantTribeTransferPermit = (characterId, delta);
        TribeTransferPermitCount += delta;
        return ValueTask.FromResult(TribeTransferPermitCount);
    }
}
