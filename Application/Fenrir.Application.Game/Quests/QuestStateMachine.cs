using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Pure port of <c>AVATAR_OBJECT::ReturnQuestPresentState</c>/<c>ReturnQuestEndConditionState</c> and
///     the 5 <c>CZ_PROCESS_QUEST_SEND</c> transitions (report 04 §5, verified against
///     <c>Server/ts25zone/S07_MyGame04.cpp:1685-2130</c> and <c>S04_MyWork02.cpp:7307-7563</c> byte-for-byte
///     -- report 04's own summary undercounts the real <c>qSort</c> space: the VERIFIED source has 8
///     branches, not 6:
///     <list type="bullet">
///         <item>1 kill monsters, 2 item acquisition, 3 item delivery, 4 receiving items, 5 kill captain,
///             6 exchange items (2 phases) -- report 04's documented 6.</item>
///         <item>7 "meet NPC" -- undocumented by report 04, fully implemented here: uniquely, its own
///             end-condition is <c>PresentState == 2</c> (not 3), i.e. simply HOLDING the matching
///             <c>TargetPhase</c> value already satisfies it -- there is no separate "meet" action, the
///             NPC-proximity check (if any) is entirely client-side.</item>
///         <item>8 "Waterfall occupation" (zone038 special-event participation) -- state-readable here for
///             completeness, but its ONLY verified increment hook
///             (<c>Server/ts25zone/S07_MyGame01.cpp:4236</c> et al.) lives inside the zone038 special-war
///             tick loop, which report 05 §13 already documents as out of Fenrir's scope -- accepting a
///             qSort-8 quest is real and correct, but <see cref="QuestProgress.KillCounter" /> can never
///             advance past 0 until that subsystem exists (open issue, not a fabricated formula).</item>
///     </list>
///     No I/O, no <see cref="Fenrir.Application.Game.World.Zone" />/<see cref="Fenrir.Application.Game.World.PlayerRuntimeState" />
///     dependency -- the inventory-presence check every item-based branch needs is injected as
///     <paramref name="hasItem" />-shaped delegates so this stays independently unit-testable, same
///     convention as <see cref="Fenrir.Application.Game.Inventory.ContainerMatrix" />/<see cref="Fenrir.Application.Game.World.Npcs.NpcShopPolicy" />.
/// </summary>
public static class QuestStateMachine
{
    public const int StateInvalid = 0;
    public const int StateCanAccept = 1;
    public const int StateInProgress = 2;
    public const int StateConditionMet = 3;
    public const int StateExchangeAwaitingReturn = 4;
    public const int StateExchangeReturnReady = 5;

    /// <summary><c>ReturnQuestPresentState</c> -- verified S07_MyGame04.cpp:1685-1878.</summary>
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

            case 7: // meet NPC -- uniquely satisfied the instant the id matches, see class remarks
                return progress.TargetPhase == (q.Solution1 ?? 0) ? StateInProgress : StateInvalid;

            case 8: // Waterfall occupation -- state-readable, increment hook out of scope (see class remarks)
                if (progress.TargetPhase != (q.Solution1 ?? 0)) return StateInvalid;
                return progress.KillCounter < 1 ? StateInProgress : StateConditionMet;

            default:
                return StateInvalid;
        }
    }

    /// <summary><c>ReturnQuestEndConditionState</c> -- verified S07_MyGame04.cpp:1880-1935. 1 = the completion action (tSort 2) is legal right now.</summary>
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

    /// <summary>tSort 1 result: an item deposit is required for qSort 3/6 (into a client-chosen EMPTY slot), null for every other qSort.</summary>
    public readonly record struct AcceptResult(bool Success, QuestProgress NewProgress, int? DepositItemId);

    /// <summary>
    ///     tSort 1, "mission issuance" (S04_MyWork02.cpp:7314-7368). Fails (mirrors legacy <c>Quit()</c>) if
    ///     <see cref="ComputePresentState" /> isn't exactly <see cref="StateCanAccept" />, or the next quest
    ///     in the chain doesn't exist -- the caller is responsible for the qSort-3/6 slot-occupancy/bounds
    ///     Quit() guards (this method does not see inventory slots, only whether an item id is present).
    /// </summary>
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

    /// <summary>tSort 2 result: reward money/CP/XP/TeacherPoint deltas (report 04 §5's qReward loop), the item id to delete from inventory (0 = none), and the optional reward-item id/quantity to deposit.</summary>
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

    /// <summary>
    ///     tSort 2, "mission completed" (S04_MyWork02.cpp:7369-7452). Fails if
    ///     <see cref="ComputeEndConditionMet" /> is false or the CURRENT quest (StepPermanent, not +1)
    ///     doesn't resolve. Reward-item resolution (<c>ReturnItemNumberForQuestReward</c>/
    ///     <c>ReturnItemQuantityForQuestReward</c>, S07_MyGame04.cpp:2132-2191) is folded in here: the
    ///     FIRST reward slot with RewardType==6 gives the item id; quantity is 0 for item.Sort in [7,29]
    ///     (equipment-like), else 1 -- <paramref name="itemSort" /> resolves that ONE lookup so this method
    ///     stays catalog-shaped rather than needing the whole WorldDataCache.
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
                // CORRECTION (review finding): reward type 5 (aTeacherPoint, GL_614_QUEST_TEACHER_POINT,
                // S04_MyWork02.cpp:7420-7423) was previously unhandled -- silently dropped despite 36 real
                // world.QuestRewards seed rows using it, several granting 100,000 points.
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

    /// <summary>
    ///     tSort 3, "mission receive" (S04_MyWork02.cpp:7453-7504) -- deposits <c>qSolution[0]</c> into a
    ///     client-chosen empty slot; does NOT mutate <see cref="QuestProgress" /> at all (verified: no
    ///     <c>wAvatar.aQuestInfo</c> write in this branch). Legal only for qSort 3/4 at present-state 2, or
    ///     qSort 6 at present-state 2 OR 4.
    /// </summary>
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

    /// <summary>
    ///     tSort 4, "mission exchange" (S04_MyWork02.cpp:7505-7528) -- <c>ChangeQuestItem(Solution1 -&gt; Solution2)</c>
    ///     (swap the ITEM ID in place, same inventory slot) plus <c>[3]=2, [4]=Solution2</c>. Legal only for
    ///     qSort 6 at present-state 3.
    /// </summary>
    public readonly record struct ExchangeResult(bool Success, QuestProgress NewProgress, int FromItemId, int ToItemId);

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

    /// <summary>
    ///     tSort 5, "mission abandonment" (S04_MyWork02.cpp:7529-7555). Legal only when NOT idle/can-accept
    ///     (present-state &gt;= 2), the quest's own <c>qType == 2</c> (abandonable), and the end condition is
    ///     NOT already met (a satisfiable quest must be completed, not abandoned).
    /// </summary>
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
}
