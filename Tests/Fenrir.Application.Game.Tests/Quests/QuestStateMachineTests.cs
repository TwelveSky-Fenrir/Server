using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Quests;

public class QuestStateMachineTests
{
    private const byte Tribe = 0;
    private const byte Category = 1;

    private static QuestCatalog Catalog(params QuestRowDto[] quests)
    {
        var rows = WorldDataTestRows.MinimalRows() with { Quests = quests, QuestRewards = [], QuestSpeeches = [] };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        return new QuestCatalog(cache);
    }

    private static QuestCatalog CatalogWithRewards(QuestRowDto quest, params QuestRewardRowDto[] rewards)
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Quests = [quest], QuestRewards = rewards, QuestSpeeches = []
        };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        return new QuestCatalog(cache);
    }

    private static bool NoItems(int _)
    {
        return false;
    }

    [Fact]
    public void PresentState_Idle_NoNextQuest_IsInvalid()
    {
        var catalog = Catalog();
        var state = QuestStateMachine.ComputePresentState(QuestProgress.None, Tribe, 100, catalog, NoItems);

        Assert.Equal(QuestStateMachine.StateInvalid, state);
    }

    [Fact]
    public void PresentState_Idle_NextQuestExists_ButLevelTooLow_IsInvalid()
    {
        var next = WorldDataTestRows.Quest(1) with { Category = Category, Step = 1, Level = 50, Sort = 1 };
        var catalog = Catalog(next);

        var state = QuestStateMachine.ComputePresentState(QuestProgress.None, Tribe, 10, catalog, NoItems);

        Assert.Equal(QuestStateMachine.StateInvalid, state);
    }

    [Fact]
    public void PresentState_Idle_NextQuestExists_LevelSufficient_CanAccept()
    {
        var next = WorldDataTestRows.Quest(1) with { Category = Category, Step = 1, Level = 5, Sort = 1 };
        var catalog = Catalog(next);

        var state = QuestStateMachine.ComputePresentState(QuestProgress.None, Tribe, 10, catalog, NoItems);

        Assert.Equal(QuestStateMachine.StateCanAccept, state);
    }

    [Fact]
    public void PresentState_KillQuest_BelowThreshold_IsInProgress()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 3
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 2);

        var state = QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, NoItems);

        Assert.Equal(QuestStateMachine.StateInProgress, state);
    }

    [Fact]
    public void PresentState_KillQuest_AtThreshold_IsConditionMet_AndEndConditionHolds()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 3
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 3);

        Assert.Equal(QuestStateMachine.StateConditionMet,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, NoItems));
        Assert.True(QuestStateMachine.ComputeEndConditionMet(progress, Tribe, 10, catalog, NoItems));
    }

    [Fact]
    public void PresentState_KillQuest_WrongMonsterId_IsInvalid()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 3
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 9999, 3);

        Assert.Equal(QuestStateMachine.StateInvalid,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, NoItems));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PresentState_ItemQuest_NotInInventory_IsInProgress(int qSort)
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 2, Sort = (byte)qSort, Solution1 = 7000
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(2, 1, qSort, 7000, 0);

        Assert.Equal(QuestStateMachine.StateInProgress,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, NoItems));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PresentState_ItemQuest_InInventory_IsConditionMet(int qSort)
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 2, Sort = (byte)qSort, Solution1 = 7000
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(2, 1, qSort, 7000, 0);

        Assert.Equal(QuestStateMachine.StateConditionMet,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, id => id == 7000));
    }

    [Fact]
    public void PresentState_CaptainQuest_ZeroKills_IsInProgress_OneKill_IsConditionMet()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 4, Sort = 5, Solution1 = 8000 };
        var catalog = Catalog(quest);

        var notYet = new QuestProgress(4, 1, 5, 8000, 0);
        var done = new QuestProgress(4, 1, 5, 8000, 1);

        Assert.Equal(QuestStateMachine.StateInProgress,
            QuestStateMachine.ComputePresentState(notYet, Tribe, 10, catalog, NoItems));
        Assert.Equal(QuestStateMachine.StateConditionMet,
            QuestStateMachine.ComputePresentState(done, Tribe, 10, catalog, NoItems));
    }

    [Fact]
    public void PresentState_WaterfallOccupationQuest_ZeroCounter_IsInProgress_OneCounter_IsConditionMet()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 4, Sort = 8, Solution1 = 38 };
        var catalog = Catalog(quest);

        var notYet = new QuestProgress(4, 1, 8, 38, 0);
        var done = new QuestProgress(4, 1, 8, 38, 1);

        Assert.Equal(QuestStateMachine.StateInProgress,
            QuestStateMachine.ComputePresentState(notYet, Tribe, 10, catalog, NoItems));
        Assert.Equal(QuestStateMachine.StateConditionMet,
            QuestStateMachine.ComputePresentState(done, Tribe, 10, catalog, NoItems));
        Assert.True(QuestStateMachine.ComputeEndConditionMet(done, Tribe, 10, catalog, NoItems));
    }

    [Fact]
    public void PresentState_WaterfallOccupationQuest_TargetPhaseNotMatchingSolution1_IsInvalid()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 4, Sort = 8, Solution1 = 38 };
        var catalog = Catalog(quest);
        var mismatch = new QuestProgress(4, 1, 8, 49, 0);

        Assert.Equal(QuestStateMachine.StateInvalid,
            QuestStateMachine.ComputePresentState(mismatch, Tribe, 10, catalog, NoItems));
    }

    [Fact]
    public void PresentState_ExchangeQuest_Phase1_ItemPresent_IsConditionMet()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 5, Sort = 6, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(5, 1, 6, 1, 100);

        Assert.Equal(QuestStateMachine.StateConditionMet,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, id => id == 100));
    }

    [Fact]
    public void PresentState_ExchangeQuest_Phase2_ItemPresent_IsReturnReady_AndEndConditionHolds()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 5, Sort = 6, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(5, 1, 6, 2, 200);

        Assert.Equal(QuestStateMachine.StateExchangeReturnReady,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, id => id == 200));
        Assert.True(QuestStateMachine.ComputeEndConditionMet(progress, Tribe, 10, catalog, id => id == 200));
    }

    [Fact]
    public void PresentState_MeetNpcQuest_MatchingId_IsInProgress_AndImmediatelyCompletable()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 6, Sort = 7, Solution1 = 9001 };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(6, 1, 7, 9001, 0);

        Assert.Equal(QuestStateMachine.StateInProgress,
            QuestStateMachine.ComputePresentState(progress, Tribe, 10, catalog, NoItems));
        Assert.True(QuestStateMachine.ComputeEndConditionMet(progress, Tribe, 10, catalog, NoItems));
    }

    [Fact]
    public void Accept_KillQuest_AdvancesStepPermanent_AndSeedsTargetFromSolution1()
    {
        var next = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 6, Level = 1, Sort = 1, Solution1 = 5001, Solution2 = 3
        };
        var catalog = Catalog(next);
        var progress = new QuestProgress(5, 0, 0, 0, 0);

        var result = QuestStateMachine.Accept(progress, Tribe, 10, catalog, NoItems);

        Assert.True(result.Success);
        Assert.Null(result.DepositItemId);
        Assert.Equal(new QuestProgress(6, 1, 1, 5001, 0), result.NewProgress);
    }

    [Fact]
    public void Accept_DeliveryQuest_qSort3_RequiresADeposit()
    {
        var next = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 1, Level = 1, Sort = 3, Solution1 = 700
        };
        var catalog = Catalog(next);

        var result = QuestStateMachine.Accept(QuestProgress.None, Tribe, 10, catalog, NoItems);

        Assert.True(result.Success);
        Assert.Equal(700, result.DepositItemId);
        Assert.Equal(3, result.NewProgress.QSort);
        Assert.Equal(700, result.NewProgress.TargetPhase);
        Assert.Equal(0, result.NewProgress.KillCounter);
    }

    [Fact]
    public void Accept_ExchangeQuest_qSort6_SeedsPhase1_AndEchoesSolution1IntoKillCounter()
    {
        var next = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 1, Level = 1, Sort = 6, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(next);

        var result = QuestStateMachine.Accept(QuestProgress.None, Tribe, 10, catalog, NoItems);

        Assert.True(result.Success);
        Assert.Equal(100, result.DepositItemId);
        Assert.Equal(1, result.NewProgress.TargetPhase);
        Assert.Equal(100, result.NewProgress.KillCounter);
    }

    [Fact]
    public void Accept_NotInCanAcceptState_Fails()
    {
        var catalog = Catalog();
        var result = QuestStateMachine.Accept(QuestProgress.None, Tribe, 10, catalog, NoItems);

        Assert.False(result.Success);
    }

    [Fact]
    public void Complete_KillQuest_ResetsProgress_ButKeepsStepPermanent_AndSumsRewardsByType()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 1
        };
        var rewards = new[]
        {
            new QuestRewardRowDto(1, 0, 2, null, 1000),
            new QuestRewardRowDto(1, 1, 3, null, 50),
            new QuestRewardRowDto(1, 2, 4, null, 200)
        };
        var catalog = CatalogWithRewards(quest, rewards);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, NoItems, _ => null);

        Assert.True(result.Success);
        Assert.Equal(1000, result.MoneyReward);
        Assert.Equal(50, result.KillOtherTribeCountReward);
        Assert.Equal(200, result.ExperienceReward);
        Assert.Equal(0, result.RewardItemId);
        Assert.Equal(new QuestProgress(3, 0, 0, 0, 0), result.NewProgress);
    }

    [Fact]
    public void Complete_RewardType5_GrantsTeacherPoint()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 1
        };
        var rewards = new[] { new QuestRewardRowDto(1, 0, 5, null, 100_000) };
        var catalog = CatalogWithRewards(quest, rewards);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, NoItems, _ => null);

        Assert.True(result.Success);
        Assert.Equal(100_000, result.TeacherPointReward);
    }

    [Fact]
    public void Complete_ItemAcquisitionQuest_DeletesTheQuestItem()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 2, Sort = 2, Solution1 = 7000 };
        var catalog = CatalogWithRewards(quest);
        var progress = new QuestProgress(2, 1, 2, 7000, 0);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, id => id == 7000, _ => null);

        Assert.True(result.Success);
        Assert.Equal(7000, result.DeleteItemId);
    }

    [Fact]
    public void Complete_RewardType6_ResolvesFirstItemSlot_AndZeroQuantityForEquipmentSorts()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 2, Sort = 2, Solution1 = 7000 };
        var rewards = new[] { new QuestRewardRowDto(1, 0, 6, 9500, null) };
        var catalog = CatalogWithRewards(quest, rewards);
        var progress = new QuestProgress(2, 1, 2, 7000, 0);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, id => id == 7000,
            itemId => itemId == 9500 ? 9 : null);

        Assert.True(result.Success);
        Assert.Equal(9500, result.RewardItemId);
        Assert.Equal(0, result.RewardItemQuantity);
    }

    [Fact]
    public void Complete_RewardType6_NonEquipmentSort_QuantityIsOne()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 2, Sort = 2, Solution1 = 7000 };
        var rewards = new[] { new QuestRewardRowDto(1, 0, 6, 9501, null) };
        var catalog = CatalogWithRewards(quest, rewards);
        var progress = new QuestProgress(2, 1, 2, 7000, 0);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, id => id == 7000,
            itemId => itemId == 9501 ? 99 : null);

        Assert.Equal(1, result.RewardItemQuantity);
    }

    [Fact]
    public void Complete_RewardType6_ItemUnresolvableInCatalog_QuantityIsZero_ButRewardItemIdStillReported()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 2, Sort = 2, Solution1 = 7000 };
        var rewards = new[] { new QuestRewardRowDto(1, 0, 6, 9999, null) };
        var catalog = CatalogWithRewards(quest, rewards);
        var progress = new QuestProgress(2, 1, 2, 7000, 0);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, id => id == 7000, _ => null);

        Assert.True(result.Success);
        Assert.Equal(9999, result.RewardItemId);
        Assert.Equal(0, result.RewardItemQuantity);
    }

    [Fact]
    public void Complete_EndConditionNotMet_Fails()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 5
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var result = QuestStateMachine.Complete(progress, Tribe, 10, catalog, NoItems, _ => null);

        Assert.False(result.Success);
    }

    [Fact]
    public void TryReceive_ItemDeliveryQuest_InProgress_ReturnsDepositItem_WithoutChangingProgress()
    {
        var quest = WorldDataTestRows.Quest(1) with { Category = Category, Step = 2, Sort = 3, Solution1 = 700 };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(2, 1, 3, 700, 0);

        var ok = QuestStateMachine.TryReceive(progress, Tribe, 10, catalog, NoItems, out var depositItemId);

        Assert.True(ok);
        Assert.Equal(700, depositItemId);
    }

    [Fact]
    public void TryReceive_WrongQSort_Fails()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 2, Sort = 1, Solution1 = 700, Solution2 = 1
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(2, 1, 1, 700, 0);

        var ok = QuestStateMachine.TryReceive(progress, Tribe, 10, catalog, NoItems, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryExchange_ConditionMet_SwapsItemId_AndAdvancesToPhase2()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 5, Sort = 6, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(5, 1, 6, 1, 100);

        var result = QuestStateMachine.TryExchange(progress, Tribe, 10, catalog, id => id == 100);

        Assert.True(result.Success);
        Assert.Equal(100, result.FromItemId);
        Assert.Equal(200, result.ToItemId);
        Assert.Equal(2, result.NewProgress.TargetPhase);
        Assert.Equal(200, result.NewProgress.KillCounter);
    }

    [Fact]
    public void TryExchange_NotYetConditionMet_Fails()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 5, Sort = 6, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(5, 1, 6, 1, 100);

        var result = QuestStateMachine.TryExchange(progress, Tribe, 10, catalog, NoItems);

        Assert.False(result.Success);
    }

    [Fact]
    public void TryExchange_WrongQSort_Fails()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 5, Sort = 1, Solution1 = 100, Solution2 = 200
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(5, 1, 1, 100, 0);

        var result = QuestStateMachine.TryExchange(progress, Tribe, 10, catalog, id => id == 100);

        Assert.False(result.Success);
    }

    [Fact]
    public void TryAbandon_AbandonableQuest_NotYetComplete_Succeeds_AndResetsButKeepsStep()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Type = 2, Solution1 = 5001, Solution2 = 5
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var ok = QuestStateMachine.TryAbandon(progress, Tribe, 10, catalog, NoItems, out var newProgress);

        Assert.True(ok);
        Assert.Equal(new QuestProgress(3, 0, 0, 0, 0), newProgress);
    }

    [Fact]
    public void TryAbandon_NonAbandonableQuest_Type1_Fails()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Type = 1, Solution1 = 5001, Solution2 = 5
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var ok = QuestStateMachine.TryAbandon(progress, Tribe, 10, catalog, NoItems, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryAbandon_AlreadyConditionMet_Fails_MustCompleteInstead()
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = Category, Step = 3, Sort = 1, Type = 2, Solution1 = 5001, Solution2 = 1
        };
        var catalog = Catalog(quest);
        var progress = new QuestProgress(3, 1, 1, 5001, 1);

        var ok = QuestStateMachine.TryAbandon(progress, Tribe, 10, catalog, NoItems, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryAbandon_IdleOrCanAcceptState_Fails()
    {
        var next = WorldDataTestRows.Quest(1) with { Category = Category, Step = 1, Level = 1, Sort = 1 };
        var catalog = Catalog(next);

        var ok = QuestStateMachine.TryAbandon(QuestProgress.None, Tribe, 10, catalog, NoItems, out _);

        Assert.False(ok);
    }
}
