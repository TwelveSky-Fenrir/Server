using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     <c>MAX_MISSION_JOIN_WAR_NUM</c> -- <see cref="PlayerRuntimeState.MissionJoinWar" />'s cap. In
    ///     practice this makes the counter a boolean "was present when a war concluded today" flag rather than
    ///     a running count (this cluster's own behavior contract, Edge cases section).
    /// </summary>
    /// <remarks>Réf. C++ : Server/Header/Protocol/DEFINE.h:400.</remarks>
    private const int MissionJoinWarCap = 1;

    /// <summary>
    ///     <see cref="QuestProgressResponse" />.Sort marker for the archetype-8 (waterfall-occupation)
    ///     war-conclusion step credit -- distinct from every marker <c>Zone.Combat.cs</c>'s monster-kill
    ///     quest-credit push uses (<see cref="QuestKillCreditArchetype1SoloSort" />/
    ///     <see cref="QuestKillCreditRelayOrArchetype5Sort" />), matching this cluster's own behavior contract
    ///     ("the marker-9 notification described above").
    /// </summary>
    private const int QuestWaterfallWarConclusionCreditSort = 9;

    /// <summary>
    ///     <see cref="ZoneCommandKind.CreditRegularWarConclusion" />'s handler -- the ONLY site permitted to
    ///     mutate <see cref="PlayerRuntimeState.MissionJoinWar" /> or drive the archetype-8 (waterfall-
    ///     occupation) war-conclusion quest-step credit, preserving the single-writer-per-zone invariant this
    ///     whole class relies on (this runs on the zone's own tick thread; the poster,
    ///     <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarSchedulerHost" />, runs on its own
    ///     background-timer thread and never touches <see cref="PlayerRuntimeState" /> directly -- same
    ///     precedent as <see cref="HandleSetMuted" />/<see cref="HandleMarkZoneTransferPending" />). A no-op if
    ///     the character already left this zone between the poster's own snapshot and this command draining.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Two independent effects, both gated purely on this character's own live state (never on
    ///         whether they personally fought in the war):
    ///     </para>
    ///     <para>
    ///         1. <see cref="PlayerRuntimeState.MissionJoinWar" /> is incremented by one, but only while it
    ///         remains below <see cref="MissionJoinWarCap" /> -- a repeat credit within the same daily cycle
    ///         (this character was already present for an earlier war conclusion today) silently no-ops.
    ///     </para>
    ///     <para>
    ///         2. If this character's active quest is archetype 8 ("waterfall occupation", qSort 8), its own
    ///         target value (<see cref="PlayerRuntimeState.QuestTargetPhase" />, set from the quest's
    ///         <c>Solution1</c> at accept time and never otherwise mutated while qSort 8 is active -- see
    ///         <see cref="Fenrir.Application.Game.Domain.Quests.QuestStateMachine.Accept" />) matches this
    ///         zone's own map id, and the present-state is exactly "state 2" (in progress -- per
    ///         <see cref="Fenrir.Application.Game.Domain.Quests.QuestStateMachine.ComputePresentState" />'s own
    ///         qSort-8 case, that collapses to <see cref="PlayerRuntimeState.QuestKillCounter" /> still below
    ///         1), the counter is incremented to 1 and a unicast <see cref="QuestProgressResponse" /> is pushed
    ///         with <see cref="QuestWaterfallWarConclusionCreditSort" /> (destination-slot fields zeroed, same
    ///         "no inventory-slot meaning" posture as every other server-pushed quest notification -- see
    ///         <see cref="ApplyQuestKillProgressToOne" />). Neither branch touches the quest catalog: qSort-8's
    ///         own end-condition collapses to this same &lt;1 clamp check
    ///         (<c>QuestStateMachine.ComputePresentState</c> case 8), so no catalog lookup is needed to
    ///         reproduce it here.
    ///     </para>
    ///     <para>
    ///         Neither effect is persisted beyond <see cref="PlayerRuntimeState.MarkProgressDirty" /> (same
    ///         "in-memory counter, durable only via write-behind's <c>DirtyFlags.Progression</c> bump keeping
    ///         other progression fields flowing" posture <see cref="ApplyPvpKillRewards" />'s own
    ///         <c>MissionKillOtherTribe</c> grant and <see cref="ApplyQuestKillProgressToOne" />'s
    ///         <c>QuestKillCounter</c> grant already have -- <c>CharacterProgressTvp</c> does not carry either
    ///         field; both are only durably captured at their own natural persistence point (a daily-mission
    ///         claim, a quest transition) -- not a gap introduced here).
    ///     </para>
    ///     <para>
    ///         <b>Scope:</b> only the Regular War (Zone049) participant reward-payout loop is wired to this
    ///         handler -- the cluster's cited primary call site
    ///         (Server/ts25zone/S07_MyGame01.cpp:5293-5314) sits inside the exact same function range
    ///         <see cref="Fenrir.Application.Game.Domain.World.ZoneWar.RegularWarRewardCalculator" /> itself
    ///         cites (:5289-5413, "the full reward-payout participant loop"). The behavior contract's three
    ///         near-duplicate call sites for other war-type state machines (Server/ts25zone/S07_MyGame01.cpp:
    ///         6396-6398, 6859-6861, 12498-12500) were carried forward from the input finding only, not
    ///         independently reopened/verified this session -- porting an equivalent hook into whichever
    ///         Fenrir state machine(s) correspond to Labyrinth (ZONE175)/Nok-San (ZONE195)/siege (ZONE200)
    ///         needs its own legacy-behavior-translator confirmation first, per this repo's no-guessing-legacy-
    ///         behavior rule; not implemented here.
    ///     </para>
    /// </remarks>
    private void HandleRegularWarConclusionCredit(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.MissionJoinWar < MissionJoinWarCap)
        {
            state.MissionJoinWar++;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (state.QuestActiveFlag == 1 && state.QuestSort == 8 && state.QuestTargetPhase == MapId &&
            state.QuestKillCounter < 1)
        {
            state.QuestKillCounter++;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            state.Session.Send(new QuestProgressResponse
            {
                Sort = QuestWaterfallWarConclusionCreditSort, Page = 0, Index = 0, XPost = 0, YPost = 0
            });
        }
    }
}
