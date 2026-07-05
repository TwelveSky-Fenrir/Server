using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     The linear per-tribe quest chain's permanent progression index (legacy <c>aQuestInfo[0]</c>) -- survives
    ///     completion/abandon.
    /// </summary>
    public int QuestStepPermanent { get; set; }

    /// <summary>
    ///     Legacy <c>aQuestInfo[1]</c> -- a 0/1 "quest active" flag, NOT a quest id despite the DB column's
    ///     legacy-derived name.
    /// </summary>
    public int QuestActiveFlag { get; set; }

    /// <summary>Legacy <c>aQuestInfo[2]</c> -- the active quest's <c>qSort</c> (1-8). 0 = no active quest.</summary>
    public int QuestSort { get; set; }

    /// <summary>Legacy <c>aQuestInfo[3]</c> -- target item id / exchange phase, meaning depends on <see cref="QuestSort" />.</summary>
    public int QuestTargetPhase { get; set; }

    /// <summary>
    ///     Legacy <c>aQuestInfo[4]</c> -- kill counter / second exchange item, meaning depends on
    ///     <see cref="QuestSort" />. Incremented by the monster-kill hook (qSort 1/5).
    /// </summary>
    public int QuestKillCounter { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aJoinWar</c> -- gates the daily-mission reward claim (&gt;= 1). Its only
    ///     verified increment hook lives inside the war-event state machines (out of scope here), so this
    ///     stays 0 for every character until that subsystem exists -- a real, correctly-gated, but currently
    ///     unreachable mechanic, not a stub.
    /// </summary>
    public int MissionJoinWar { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aKillOtherTribe</c> -- a separate counter from <see cref="ContributionPoints" />,
    ///     gates the daily-mission claim (&gt;= 10). Incremented by <c>Zone.ApplyPvpKillMissionProgress</c>, gated
    ///     by <see cref="KillCooldownTracker" /> so repeat-farming one victim only
    ///     counts once per cooldown window (C05); the CP/EXP/drop side of a PvP kill's reward is still not
    ///     implemented.
    /// </summary>
    public int MissionKillOtherTribe { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aKillMonster</c> -- tracked (echoed on ZC 163) but its own claim-gate is compiled out
    ///     in EU33, so it never blocks a claim.
    /// </summary>
    public int MissionKillMonster { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aPlayTime</c> -- same "tracked, gate compiled out" posture as
    ///     <see cref="MissionKillMonster" />.
    /// </summary>
    public int MissionPlayTime { get; set; }
}
