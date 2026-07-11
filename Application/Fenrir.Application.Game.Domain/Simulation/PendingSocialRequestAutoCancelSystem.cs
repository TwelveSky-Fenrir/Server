using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Per-tick pending-social-request auto-cancel (behavior contract C21§G): every online avatar's pending
///     ask/invite in each of 5 independent negotiation families -- trade (<see cref="TradeRegistry" />),
///     friend (<see cref="FriendRegistry" />), teacher/mentor (<see cref="MentorRegistry" />), party
///     (<see cref="PartyRegistry" />), and guild (<see cref="GuildInviteRegistry" />) -- is silently reset
///     back to "none" the instant the negotiation's counterpart can no longer be found anywhere on this
///     shard. Deliberately excludes duel: contract C21's own Edge cases G notes duel is NOT among the 5,
///     despite being one of the 7 "busy" states the community-work gate (contract C21§H) checks, with no
///     stated legacy rationale for the asymmetry -- preserved as an observed fact, not resolved by inventing
///     one (do not widen this system to Duel without a fresh legacy citation).
/// </summary>
/// <remarks>
///     Réf. C++ (via behavior contract C21§G, not independently re-opened this session):
///     Server/ts25zone/S07_MyGame04.cpp:751-785 (the per-tick sweep, inside the general avatar update
///     routine, independently for each of the 5 pending-request state fields).
///     <para>
///         <b>
///             "Counterpart could no longer be found" == not currently resolvable anywhere on this
///             GameServer shard process
///         </b>
///         , via <see cref="ZoneRegistry.TryGetPlayer" /> -- matching the scope
///         every one of these 5 registries is itself already documented as sharing ("process-wide... a
///         party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors within THIS
///         process", <c>DomainServiceCollectionExtensions.AddGameDomain</c>'s own remarks). A still-pending
///         entry where BOTH sides have gone offline is not visited by this sweep at all (neither side's own
///         <c>zone.Players</c> enumeration reaches it) -- the same "only cleaned up while at least one side
///         is still online to notice" characteristic <see cref="DuelMaintenanceSystem" /> and every other
///         per-online-avatar maintenance system in this cluster already has; not treated as a defect here.
///     </para>
///     <para>
///         <b>Only the PENDING-ask state is swept</b> -- deliberately excludes each registry's own later
///         "accepted, awaiting finalize" state (<c>TradeRegistry</c>'s <c>_acceptedPairs</c>,
///         <c>FriendRegistry</c>/<c>MentorRegistry</c>/<c>GuildInviteRegistry</c>'s own <c>_acceptedFor</c>,
///         and an actually-open <c>TradeSession</c>), matching Precondition G's own "a DIFFERENT value from
///         the 'mid-trade, confirmed' value [contract] F uses, even though both values live on the same
///         underlying trade-process-state field at different points in the trade lifecycle." Each registry's
///         own <c>TryPeekPending</c> reads ONLY its <c>_pendingByAsker</c>/<c>_pendingByTarget</c>-shaped
///         dictionaries for exactly this reason.
///     </para>
///     <para>
///         <b>Cancellation mechanics reuse each registry's own existing, already-public API</b> -- no
///         registry needed a new mutating method, only a new non-consuming peek (<c>TryPeekPending</c>, added
///         to all 5 registries as a purely-additive sibling to their own pre-existing <c>IsBusy</c>/
///         <c>IsNegotiating</c> read, per this contract slice's own wiringManifest). Cancelling from the
///         ASKER side reuses <c>TryCancel(askerId, ...)</c> (already existed for the voluntary-cancel opcode
///         handlers); cancelling from the TARGET side reuses <c>TryAnswer(targetId, accepted: false, ...)</c>
///         (already existed for the voluntary-decline opcode handlers) -- both already perform the identical
///         dictionary cleanup with NO notification sent by the registry itself, matching Side effects G
///         exactly ("No notification is sent to anyone as part of this reset").
///     </para>
///     <para>
///         <b><see cref="Lazy{ZoneRegistry}" />, not a direct <see cref="ZoneRegistry" /> dependency</b> --
///         same "breaks the ZoneRegistry constructor-graph cycle" reason
///         <see cref="World.ZoneWar.ValleyWarSystem" />/<c>Zone195NokSanSystem</c> already document: this
///         system is itself one of the <see cref="ISimulationSystem" /> instances <see cref="ZoneRegistry" />
///         resolves at its own construction time, so a direct (non-<c>Lazy</c>) dependency back on
///         <see cref="ZoneRegistry" /> would be a same-container DI cycle. The existing
///         <c>services.AddSingleton(sp =&gt; new Lazy&lt;ZoneRegistry&gt;(sp.GetRequiredService&lt;ZoneRegistry&gt;))</c>
///         registration (<c>HostingServiceCollectionExtensions.AddZoneWar</c>) is reused as-is; see this
///         contract slice's own wiringManifest for the exact new-system DI registration.
///     </para>
/// </remarks>
public sealed class PendingSocialRequestAutoCancelSystem(
    TradeRegistry tradeRegistry,
    FriendRegistry friendRegistry,
    MentorRegistry mentorRegistry,
    PartyRegistry partyRegistry,
    GuildInviteRegistry guildInviteRegistry,
    Lazy<ZoneRegistry> zoneRegistry) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            var characterId = state.CharacterId;

            SweepTrade(characterId);
            SweepFriend(characterId);
            SweepMentor(characterId);
            SweepParty(characterId);
            SweepGuildInvite(characterId);
        }
    }

    private bool IsReachable(int characterId)
    {
        return zoneRegistry.Value.TryGetPlayer(characterId, out _);
    }

    private void SweepTrade(int characterId)
    {
        if (!tradeRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            tradeRegistry.TryCancel(characterId, out _);
        else
            tradeRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepFriend(int characterId)
    {
        if (!friendRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            friendRegistry.TryCancel(characterId, out _);
        else
            friendRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepMentor(int characterId)
    {
        if (!mentorRegistry.TryPeekPending(characterId, out var counterpartId, out var isMaster) ||
            IsReachable(counterpartId))
            return;

        if (isMaster)
            mentorRegistry.TryCancel(characterId, out _);
        else
            mentorRegistry.TryAnswer(characterId, false, out _);
    }

    private void SweepParty(int characterId)
    {
        if (!partyRegistry.TryPeekPending(characterId, out var counterpartId, out var isInviter) ||
            IsReachable(counterpartId))
            return;

        if (isInviter)
            partyRegistry.TryCancel(characterId, out _);
        else
            partyRegistry.TryAnswer(characterId, false, out _, out _);
    }

    private void SweepGuildInvite(int characterId)
    {
        if (!guildInviteRegistry.TryPeekPending(characterId, out var counterpartId, out var isAsker) ||
            IsReachable(counterpartId))
            return;

        if (isAsker)
            guildInviteRegistry.TryCancel(characterId, out _);
        else
            guildInviteRegistry.TryAnswer(characterId, false, out _);
    }
}
