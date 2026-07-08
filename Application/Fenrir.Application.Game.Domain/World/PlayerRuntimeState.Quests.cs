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
    ///     Legacy <c>aMissionDate.aJoinWar</c> -- gates the daily-mission reward claim (&gt;= 1). Incremented
    ///     (capped at 1) by <see cref="Zone.HandleRegularWarConclusionCredit" /> for every ready,
    ///     non-zone-transferring character present in a map when that map's own Regular War (Zone049)
    ///     concludes -- see that method's own remarks for the citation and for why only Regular War, not the
    ///     three uncited near-duplicate legacy call sites for other war-type state machines, is wired today.
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
    ///     Seeded at world entry from the character's persisted Current-period total (legacy
    ///     <c>MyDB::GetHeroPoint</c>, Server/ts25login/S08_MyDB.cpp:1178-1188 -&gt;
    ///     Server/ts25playuser/S07_MyGame01.cpp:1002's unconditional copy into the zone's own live per-character
    ///     state) by <see cref="Fenrir.Application.Game.Services.ZoneLifecycle.EnterWorldService" />, threaded
    ///     through <see cref="PlayerEnterData.HeroRankPoints" />, not merely started at 0 -- so this mirror
    ///     reflects the character's true accumulated total from the instant of registration, exactly like the
    ///     legacy field it stands in for. Fenrir performs this read Zone-side (inside EnterWorldService,
    ///     alongside its other per-character world-entry reads) rather than Login-side at the wire trigger
    ///     legacy uses (DEMAND_ZONE_SERVER_INFO_SEND) threaded through the session-ticket hand-off -- see
    ///     Migrations/030_hero_rank_points_world_entry_hydration.sql's own header for why that deviation is
    ///     accepted; the seeded value is identical either way, since nothing can grant this character a point
    ///     between the two reads. An in-process zone transfer carries the LIVE value through instead
    ///     (<see cref="Fenrir.Application.Game.Domain.World.ZoneTransfer.CreateEnterData" />), so points earned
    ///     earlier in the same session are never lost to a same-shard hop. Also force-reset to 0 by
    ///     <c>Zone.ApplyHeroRankingRolloverReset</c> on the weekly Current-&gt;Previous rollover, for any
    ///     character connected at that moment whose live total was greater than 0 (with a matching
    ///     <c>S904UPDATE_HERO_POINT</c> push to that same connection) -- see that method's own remarks.
    /// </summary>
    public int HeroRankPoints { get; set; }
}
