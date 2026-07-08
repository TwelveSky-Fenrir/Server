namespace Fenrir.Application.Game.Domain.Quests;

/// <summary>
///     Port of AVATAR_OBJECT::ReturnQuestPresentState/ReturnQuestEndConditionState and the 5
///     CZ_PROCESS_QUEST_SEND transitions (S07_MyGame04.cpp:1685-2130, S04_MyWork02.cpp:7307-7563). The source
///     has 8 qSort branches, not the 6 documented elsewhere -- see cases 7/8.
/// </summary>
public static class QuestStateMachine
{
    public const int StateInvalid = 0;
    public const int StateCanAccept = 1;
    public const int StateInProgress = 2;
    public const int StateConditionMet = 3;
    public const int StateExchangeAwaitingReturn = 4;
    public const int StateExchangeReturnReady = 5;

    public static int ComputePresentState(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem)
    {
        if (progress.IsIdle)
        {
            var next = catalog.TryGet(tribe, progress.StepPermanent + 1);
            if (next is null)
                return StateInvalid;
            return level < next.Quest.Level ? StateInvalid : StateCanAccept;
        }

        if (progress.ActiveFlag != 1)
            return StateInvalid;

        var quest = catalog.TryGet(tribe, progress.StepPermanent);
        if (quest is null)
            return StateInvalid;

        var q = quest.Quest;
        switch (progress.QSort)
        {
            case 1: // kill monsters
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < (q.Solution2 ?? 0) ? StateInProgress : StateConditionMet;

            case 2: // item acquisition
            case 3: // item delivery
            case 4: // receiving items
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return hasItem(q.Solution1 ?? 0) ? StateConditionMet : StateInProgress;

            case 5: // kill the captain
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < 1 ? StateInProgress : StateConditionMet;

            case 6: // exchange items (2 phases)
                switch (progress.TargetPhase)
                {
                    case 1: // before exchange
                        if (progress.KillCounter != (q.Solution1 ?? 0)) return StateInvalid;
                        return hasItem(q.Solution1 ?? 0) ? StateConditionMet : StateInProgress;
                    case 2: // after exchange
                        if (progress.KillCounter != (q.Solution2 ?? 0)) return StateInvalid;
                        return hasItem(q.Solution2 ?? 0) ? StateExchangeReturnReady : StateExchangeAwaitingReturn;
                    default:
                        return StateInvalid;
                }

            case 7
                : // meet NPC: end condition is PresentState == 2, holding the matching TargetPhase already satisfies it
                return progress.TargetPhase == (q.Solution1 ?? 0) ? StateInProgress : StateInvalid;

            case 8: // "Waterfall occupation" (zone038 event): its own live-occupation increment hook still
                // lives in the zone038 tick loop (out of Fenrir's scope). A second, independently-cited
                // increment hook DOES exist now -- Zone.HandleRegularWarConclusionCredit credits any
                // holder present when a Regular War (Zone049) map whose id matches this quest's own
                // TargetPhase concludes (Server/ts25zone/S07_MyGame01.cpp:5293-5314) -- so KillCounter can
                // advance to 1 via that path even though the zone038-native path remains unported.
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < 1 ? StateInProgress : StateConditionMet;

            default:
                return StateInvalid;
        }
    }

    public static bool ComputeEndConditionMet(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem)
    {
        var state = ComputePresentState(progress, tribe, level, catalog, hasItem);
        return progress.QSort switch
        {
            1 or 2 or 3 or 4 or 5 or 8 => state == StateConditionMet,
            6 => state == StateExchangeReturnReady,
            7 => state == StateInProgress,
            _ => false
        };
    }

    /// <summary>Caller owns the qSort-3/6 slot-occupancy/bounds guards; this only checks whether an item id is present.</summary>
    public static AcceptResult Accept(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem)
    {
        if (ComputePresentState(progress, tribe, level, catalog, hasItem) != StateCanAccept)
            return new AcceptResult(false, progress, null);

        var next = catalog.TryGet(tribe, progress.StepPermanent + 1);
        if (next is null)
            return new AcceptResult(false, progress, null);

        var q = next.Quest;
        var qSort = q.Sort;
        var depositItemId = qSort is 3 or 6 ? q.Solution1 ?? 0 : (int?)null;

        var newProgress = new QuestProgress(
            progress.StepPermanent + 1,
            1,
            qSort,
            qSort != 6 ? q.Solution1 ?? 0 : 1,
            qSort != 6 ? 0 : q.Solution1 ?? 0);

        return new AcceptResult(true, newProgress, depositItemId);
    }

    /// <summary>
    ///     Reward-item resolution mirrors ReturnItemNumberForQuestReward/ReturnItemQuantityForQuestReward:
    ///     first RewardType==6 slot gives the item id; quantity is 0 for equipment-like items (Sort 7-29), else 1.
    /// </summary>
    public static CompleteResult Complete(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem, Func<int, byte?> itemSort)
    {
        if (!ComputeEndConditionMet(progress, tribe, level, catalog, hasItem))
            return default;

        var quest = catalog.TryGet(tribe, progress.StepPermanent);
        if (quest is null)
            return default;

        var q = quest.Quest;

        long money = 0;
        var cp = 0;
        var exp = 0;
        var teacherPoint = 0;
        var rewardItemId = 0;
        var rewardQuantity = 0;

        foreach (var reward in quest.Rewards)
            switch (reward.RewardType)
            {
                case 2: money += reward.Amount ?? 0; break;
                case 3: cp += reward.Amount ?? 0; break;
                case 4: exp += reward.Amount ?? 0; break;
                case 5: teacherPoint += reward.Amount ?? 0; break;
                case 6:
                    if (rewardItemId == 0 && reward.ItemId is { } id)
                    {
                        rewardItemId = id;
                        var sort = itemSort(id);
                        rewardQuantity = sort is >= 7 and <= 29 ? 0 : 1;
                    }

                    break;
            }

        var deleteItemId = progress.QSort switch
        {
            2 or 3 or 4 => q.Solution1 ?? 0,
            6 => q.Solution2 ?? 0,
            _ => 0
        };

        var reset = progress with { ActiveFlag = 0, QSort = 0, TargetPhase = 0, KillCounter = 0 };
        return new CompleteResult(true, reset, money, cp, exp, deleteItemId, rewardItemId, rewardQuantity,
            teacherPoint);
    }

    /// <summary>Deposits qSolution[0] into a client-chosen empty slot; does not mutate QuestProgress.</summary>
    public static bool TryReceive(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem, out int depositItemId)
    {
        depositItemId = 0;
        var state = ComputePresentState(progress, tribe, level, catalog, hasItem);

        var legal = progress.QSort switch
        {
            3 or 4 => state == StateInProgress,
            6 => state is StateInProgress or StateExchangeAwaitingReturn,
            _ => false
        };
        if (!legal)
            return false;

        var quest = catalog.TryGet(tribe, progress.StepPermanent);
        if (quest is null)
            return false;

        depositItemId = quest.Quest.Solution1 ?? 0;
        return true;
    }

    public static ExchangeResult TryExchange(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem)
    {
        if (progress.QSort != 6 ||
            ComputePresentState(progress, tribe, level, catalog, hasItem) != StateConditionMet)
            return default;

        var quest = catalog.TryGet(tribe, progress.StepPermanent);
        if (quest is null)
            return default;

        var q = quest.Quest;
        var newProgress = progress with { TargetPhase = 2, KillCounter = q.Solution2 ?? 0 };
        return new ExchangeResult(true, newProgress, q.Solution1 ?? 0, q.Solution2 ?? 0);
    }

    /// <summary>Legal only when not idle/can-accept, qType == 2 (abandonable), and the end condition isn't already met.</summary>
    public static bool TryAbandon(QuestProgress progress, byte tribe, short level, QuestCatalog catalog,
        Func<int, bool> hasItem, out QuestProgress newProgress)
    {
        newProgress = progress;
        var state = ComputePresentState(progress, tribe, level, catalog, hasItem);
        if (state is StateInvalid or StateCanAccept)
            return false;

        var quest = catalog.TryGet(tribe, progress.StepPermanent);
        if (quest is null || quest.Quest.Type != 2)
            return false;

        if (ComputeEndConditionMet(progress, tribe, level, catalog, hasItem))
            return false;

        newProgress = progress with { ActiveFlag = 0, QSort = 0, TargetPhase = 0, KillCounter = 0 };
        return true;
    }

    public readonly record struct AcceptResult(bool Success, QuestProgress NewProgress, int? DepositItemId);

    public readonly record struct CompleteResult(
        bool Success,
        QuestProgress NewProgress,
        long MoneyReward,
        int ContributionPointsReward,
        int ExperienceReward,
        int DeleteItemId,
        int RewardItemId,
        int RewardItemQuantity,
        int TeacherPointReward = 0);

    public readonly record struct ExchangeResult(bool Success, QuestProgress NewProgress, int FromItemId, int ToItemId);
}
