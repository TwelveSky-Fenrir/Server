using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Handler for the Zone038 (Holy Stone / "Waterfall") war-conclusion qSort-8 occupation credit -- the
    ///     zone-local twin of <see cref="HandleRegularWarConclusionCredit" />, driven at Stone-capture time by
    ///     <see cref="Fenrir.Application.Game.Domain.World.ZoneWar.HolyStoneWarCycle" /> (which posts one
    ///     command per qualifying character rather than mutating <see cref="PlayerRuntimeState" /> from its own
    ///     background thread -- same single-writer-per-zone posture as
    ///     <see cref="HandleRegularWarConclusionCredit" />/<see cref="HandleSetMuted" />). A no-op if the
    ///     character already left this zone between the poster's own snapshot and this command draining.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the credit the C++ applies at the Zone038 (Holy Stone) conclusion participant loop, which
    ///         is the SAME qSort-8 rule the already-ported Regular War conclusion applies -- differing only in
    ///         that war's participant filter (winning-tribe-only and alive here, versus "everyone present
    ///         regardless of win/loss" for Regular War). A prior revision of
    ///         <see cref="Fenrir.Application.Game.Domain.Quests.QuestStateMachine" /> mislabelled this Zone038
    ///         increment as "out of Fenrir's scope"; it is in fact citeable and is what this method implements.
    ///     </para>
    ///     <para>
    ///         Participant filter (contract "Holy-Stone / Waterfall (Zone038) conclusion" edge case,
    ///         Server/ts25zone/S07_MyGame01.cpp:4212-4241): only characters whose tribe equals the winning tribe
    ///         (<paramref name="winningTribe" />, :4224-4227) and who are alive (:4228-4231) receive anything --
    ///         losing-tribe members and dead characters are skipped. Re-validated here on this zone's own tick
    ///         thread (the authoritative reader of <see cref="PlayerRuntimeState.Tribe" />/
    ///         <see cref="PlayerRuntimeState.IsDead" />) even though the poster pre-filters the same fields, and
    ///         also excludes a mid-zone-transfer character (<see cref="PlayerRuntimeState.IsMovingZone" />), the
    ///         same "ready / non-transferring" guard the Regular War poster applies before posting.
    ///     </para>
    ///     <para>
    ///         Unlike <see cref="HandleRegularWarConclusionCredit" />, this site does NOT touch
    ///         <see cref="PlayerRuntimeState.MissionJoinWar" />: the co-located daily-mission "join war"
    ///         increment exists only at the Regular War / Zone053 conclusion loops (:5305-5308, :6396-6398,
    ///         :6859-6861), never at the Holy-Stone (:4236) or Zone051 (:5969) credit sites -- so it is
    ///         deliberately absent here.
    ///     </para>
    ///     <para>
    ///         The qSort-8 credit itself (contract "Side effects", :4236-4240): if this character's active quest
    ///         is archetype 8 ("occupation of WaterFall"), its bound target value
    ///         (<see cref="PlayerRuntimeState.QuestTargetPhase" />, set from the quest's <c>Solution1</c> at
    ///         accept time -- see
    ///         <see cref="Fenrir.Application.Game.Domain.Quests.QuestStateMachine.Accept" />) matches this zone's
    ///         own map id, and its present-state is exactly "state 2" (in progress -- per the qSort-8 present-
    ///         state rule at Server/ts25zone/S07_MyGame04.cpp:1865-1874 that collapses to
    ///         <see cref="PlayerRuntimeState.QuestKillCounter" /> still below 1), the counter is incremented once
    ///         (0 -&gt; 1) and a unicast <see cref="QuestProgressResponse" /> is pushed with
    ///         <see cref="QuestWaterfallWarConclusionCreditSort" /> (all slot/page/position fields zeroed -- it
    ///         conveys "your occupation quest advanced", not any inventory-slot meaning). One-shot per quest
    ///         instance: once the counter reaches 1 the present-state flips to Condition-Met (3), so a later
    ///         Zone038 conclusion in the same zone can never re-fire it.
    ///     </para>
    ///     <para>
    ///         Neither effect is persisted beyond <see cref="PlayerRuntimeState.MarkProgressDirty" /> -- same
    ///         "in-memory counter, durable only via write-behind's <c>DirtyFlags.Progression</c> bump keeping
    ///         other progression fields flowing" posture <see cref="HandleRegularWarConclusionCredit" /> already
    ///         relies on; the counter is durably captured at its own natural persistence point (the quest
    ///         transition), not here.
    ///     </para>
    ///     <para>
    ///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:4212-4241 (Zone038 conclusion participant loop and
    ///         qSort-8 credit) ; :4236-4240 (the credit itself) ; Server/ts25zone/S07_MyGame04.cpp:1865-1874
    ///         (qSort-8 present-state rule) ; Server/Header/Protocol/STRUCT.h:366 (<c>aQuestInfo[]</c>, the fields
    ///         read and mutated).
    ///     </para>
    /// </remarks>
    private void HandleZone038OccupationCredit(int characterId, byte winningTribe)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        // Zone038 participant filter: winning tribe, alive, not mid-zone-transfer. Losing-tribe members and
        // dead characters receive nothing.
        if (state.Tribe != winningTribe || state.IsDead || state.IsMovingZone)
            return;

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
