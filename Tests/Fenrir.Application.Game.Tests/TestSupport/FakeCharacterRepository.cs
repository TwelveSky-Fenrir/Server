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
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    /// <summary>Every row ever passed to <see cref="PersistPositionsAsync" />, across every call -- append-only.</summary>
    public List<CharacterPositionTvp> PersistedPositionRows { get; } = [];

    public ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        PersistedPositionRows.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ReplaceTwoContainersAsync(int characterId, byte containerA,
        IReadOnlyList<CharacterItemSlotTvp> itemsA, byte containerB, IReadOnlyList<CharacterItemSlotTvp> itemsB,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    /// <summary>Every row ever passed to <see cref="PersistProgressAsync" />, across every call -- append-only.</summary>
    public List<CharacterProgressTvp> PersistedProgressRows { get; } = [];

    public ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct)
    {
        PersistedProgressRows.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        throw new NotImplementedException();
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

    public ValueTask ExecuteTradeAsync(int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0,
        IReadOnlyList<CharacterItemSlotTvp> itemsA1, long deltaMoneyA, int deltaBigMoneyA, int characterB,
        IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1, long deltaMoneyB,
        int deltaBigMoneyB, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ApplyQuestTransitionAsync(int characterId, int stepPermanent, int activeQuestId, int qSort,
        int targetPhase, int killCounter, long deltaMoney, byte? container1,
        IReadOnlyList<CharacterItemSlotTvp> items1, byte? container2, IReadOnlyList<CharacterItemSlotTvp> items2,
        CancellationToken ct)
    {
        throw new NotImplementedException();
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

    public int TribeTransferPermitCount { get; private set; }

    public (int CharacterId, int Delta)? LastGrantTribeTransferPermit { get; private set; }

    public ValueTask<int> GrantTribeTransferPermitAsync(int characterId, int delta, CancellationToken ct)
    {
        LastGrantTribeTransferPermit = (characterId, delta);
        TribeTransferPermitCount += delta;
        return ValueTask.FromResult(TribeTransferPermitCount);
    }
}
