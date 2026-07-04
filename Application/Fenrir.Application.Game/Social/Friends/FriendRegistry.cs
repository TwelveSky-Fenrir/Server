namespace Fenrir.Application.Game.Social.Friends;

/// <summary>Soft outcomes of CZ_FRIEND_ASK_SEND -- mirrors ZC_FRIEND_ANSWER_RECV's pre-check codes (contracts/05_social.md).</summary>
public enum FriendAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide friend-request negotiation authority (Phase C/V6 Social, CZ_FRIEND_* family). The
///     actual 10-slot friend LIST is NOT owned here -- it is durable, per-character state
///     (game.CharacterFriends, cached on <c>PlayerRuntimeState.Friends</c>, mutated directly by
///     <c>FriendAddHandler</c>/<c>FriendRemoveHandler</c> exactly like <c>GenericActionHandler</c> already
///     mutates <c>PlayerRuntimeState.Inventory</c> from the owning character's own request thread -- see
///     that handler's own remarks for the established precedent this follows). This registry only tracks
///     the EPHEMERAL ask/answer handshake: same shape as <c>Party.PartyRegistry</c>'s own negotiation
///     dictionaries, with one addition -- <see cref="_acceptedFor" /> survives PAST the answer (legacy
///     state 3, contracts/05_social.md CZ_FRIEND_MAKE_SEND: "Exige état 3 (accepté)"), because unlike
///     party accept (which performs the join immediately, server-side), a friend accept does NOT
///     automatically add anything -- EACH side must separately send its OWN CZ_FRIEND_MAKE_SEND
///     afterwards, at its own pace, into a slot IT chooses. Consumed (cleared) on that character's own
///     successful add -- a documented, reasonable reading of "état 3" absent a fully re-derived state
///     machine from the source for this one corner (open issue).
/// </summary>
public sealed class FriendRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>characterId -&gt; the OTHER character it may now add via its own FriendMake (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    private bool IsNegotiating(int characterId)
    {
        return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId);
    }

    public FriendAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return FriendAskOutcome.AskerBusy;
            if (IsNegotiating(targetId))
                return FriendAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return FriendAskOutcome.Sent;
        }
    }

    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (!_pendingByAsker.Remove(askerId, out targetId))
                return false;

            _pendingByTarget.Remove(targetId);
            return true;
        }
    }

    /// <summary>CZ_FRIEND_ANSWER_SEND. On accept, BOTH sides become eligible to call their own FriendMake (<see cref="TryConsumeAccepted" />).</summary>
    public bool TryAnswer(int targetId, bool accepted, out int askerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            _pendingByAsker.Remove(askerId);

            if (accepted)
            {
                _acceptedFor[askerId] = targetId;
                _acceptedFor[targetId] = askerId;
            }

            return true;
        }
    }

    /// <summary>
    ///     CZ_FRIEND_MAKE_SEND's own precondition ("exige état 3") -- CZ_FRIEND_MAKE_SEND's wire payload
    ///     carries only a slot INDEX, no name/id, so the "other" character is whichever one
    ///     <paramref name="characterId" /> most recently had an accepted answer with (at most one at a
    ///     time in this model -- see class remarks). Consumed (cleared) on success.
    /// </summary>
    public bool TryConsumeAccepted(int characterId, out int otherId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(characterId, out otherId);
        }
    }
}
