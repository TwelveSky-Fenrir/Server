using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>
///     Mutation-triggered trade-window resync push (behavior contract C21§F).
///     <b>
///         NOT a per-tick/periodic
///         broadcast
///     </b>
///     -- see that section's own explicit correction (Edge cases F): the legacy resync step is
///     invoked synchronously from each of 9 trade-offer-mutation request-packet handlers (add/remove item,
///     money, "big money" chunk), immediately after that handler commits its own local
///     <see cref="TradeOfferSide" /> mutation, never from the avatar's per-tick update routine or any other
///     periodic path.
/// </summary>
/// <remarks>
///     Réf. C++ (via behavior contract C21§F, not independently re-opened this session):
///     Server/ts25zone/S07_MyGame04.cpp:1660-1683 (the resync step itself); Server/ts25zone/
///     S04_MyWork05.cpp:2243,2355,2453,2494,2535,3619,3661 (the 7 live call sites).
///     <para>
///         <b>No live call site exists in Fenrir yet.</b> The 9 trade-offer-mutation request handlers
///         (legacy tSort 218-222, <c>S04_MyWork05.cpp</c>) are not wired into <c>GenericActionHandler</c> --
///         confirmed this session by grepping <c>GenericActionHandler.cs</c> for a matching tSort branch
///         (none exists) and cross-checked against <see cref="TradeRegistry" />'s own class remarks: "the
///         mechanism that populates a session's offer slots (legacy tSort 218-222) is not wired into
///         GenericActionHandler yet -- a trade can be negotiated and committed end-to-end, but only with
///         whatever slots/money a caller sets directly on TradeSession." This type is forward scaffolding,
///         ready for that follow-up contract to call once it lands -- see this contract slice's own
///         wiringManifest note.
///     </para>
///     <para>
///         <b>PRECONDITION SUBSTITUTION (documented, not silently made).</b> Legacy's own precondition is
///         "both parties currently at trade-process-state 4 (mid-trade, confirmed) AND each side's cached
///         counterpart NAME matches the other side's actual name, checked in both directions" -- a
///         desync-guard against legacy's own per-avatar cached-name field. Fenrir's <see cref="TradeSession" />
///         has no equivalent cached name to desync: it is keyed directly by both participants'
///         <c>CharacterId</c>, resolved once at <see cref="TradeRegistry.TryStart" /> and never re-derived
///         from a name lookup. The structural equivalent enforced by <see cref="TryNotifyOpponent" /> is
///         therefore "the registry still holds an ACTIVE session for both
///         <see cref="TradeSession.PlayerAId" />/<see cref="TradeSession.PlayerBId" />, and it is the SAME
///         session instance both sides currently resolve to" -- re-derived fresh from
///         <see cref="TradeRegistry.TryGetSession" /> rather than trusting a caller-held, possibly-stale
///         <see cref="TradeSession" /> reference. This mirrors the same kind of legacy-per-avatar-cached-field
///         structural substitution <see cref="Party.PartyRegistry" />'s own remarks already make for a
///         different field (party-changed notification keyed by leader CharacterId instead of a cached party
///         name) -- an intentional, documented design choice, not an invented shortcut.
///     </para>
/// </remarks>
public static class TradeOfferResyncNotifier
{
    /// <summary>
    ///     Sends the mutating side's current offer (<see cref="TradeOfferCodec.BuildUpdate" />) to the OTHER
    ///     trade party only -- never echoed back to the mutating side itself (Outputs F). Silently does
    ///     nothing and returns false if either party no longer resolves to the SAME active session, or the
    ///     opponent cannot currently be found anywhere on this shard (Error/failure semantics F: "a silent
    ///     no-op gate ... no error signal to either party").
    /// </summary>
    public static bool TryNotifyOpponent(TradeRegistry tradeRegistry, ZoneRegistry zoneRegistry,
        int mutatingCharacterId)
    {
        if (!tradeRegistry.TryGetSession(mutatingCharacterId, out var session) || session is null)
            return false;

        var opponentId = session.OpponentOf(mutatingCharacterId);

        // Re-derive the opponent's own current session from the registry rather than trusting the caller's
        // reference -- the structural stand-in for legacy's bidirectional state==4/cached-name re-check (see
        // this type's own remarks).
        if (!tradeRegistry.TryGetSession(opponentId, out var opponentSession) ||
            !ReferenceEquals(opponentSession, session))
            return false;

        if (!zoneRegistry.TryGetPlayer(opponentId, out var opponent))
            return false;

        var mutatingSide = session.SideOf(mutatingCharacterId);
        opponent.Session.Send(TradeOfferCodec.BuildUpdate(mutatingSide));
        return true;
    }
}
