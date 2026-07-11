namespace Fenrir.Application.Game.Domain.Quests;

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
            case 1:
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < (q.Solution2 ?? 0) ? StateInProgress : StateConditionMet;

            case 2:
            case 3:
            case 4:
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return hasItem(q.Solution1 ?? 0) ? StateConditionMet : StateInProgress;

            case 5:
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < 1 ? StateInProgress : StateConditionMet;

            case 6:
                switch (progress.TargetPhase)
                {
                    case 1:
                        if (progress.KillCounter != (q.Solution1 ?? 0)) return StateInvalid;
                        return hasItem(q.Solution1 ?? 0) ? StateConditionMet : StateInProgress;
                    case 2:
                        if (progress.KillCounter != (q.Solution2 ?? 0)) return StateInvalid;
                        return hasItem(q.Solution2 ?? 0) ? StateExchangeReturnReady : StateExchangeAwaitingReturn;
                    default:
                        return StateInvalid;
                }

            case 7
                :
                return progress.TargetPhase == (q.Solution1 ?? 0) ? StateInProgress : StateInvalid;

            case 8:
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
