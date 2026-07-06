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
    ///     gates the daily-mission claim (&gt;= 10). Incremented by <c>Zone.ApplyPvpKillRewards</c>, gated by
    ///     <see cref="KillCooldownTracker" /> (C05, repeat-farming one victim only counts once per cooldown
    ///     window) AND, per <c>PvpKillRewardZoneCatalog</c>, by the killing zone's own reward profile -- not
    ///     every zone grants daily-mission progress (e.g. the FFA map never does).
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

    /// <summary>
    ///     Zone-side, in-memory running mirror of hero-rank points earned this session via
    ///     <c>Zone.ApplyPvpKillHeroPoints</c> (<c>MyCenterCom::AddHeroRankPoint</c>,
    ///     S06_MyUpperCom02.cpp:774-820) -- incremented synchronously the instant a grant happens, while the
    ///     durable game.HeroRankings row is updated later by
    ///     <see cref="Fenrir.Application.Game.Domain.Progression.HeroRankPointAccumulator" />'s periodic flush.
    ///     Starts at 0 for every session:
    ///     hydrating this from the character's pre-existing Current-period total at world entry is not wired
    ///     yet, so this mirror under-reports a character's true lifetime Current-period score until that
    ///     lands -- it only ever reflects points earned since the last world entry.
    /// </summary>
    public int HeroRankPoints { get; set; }
}
