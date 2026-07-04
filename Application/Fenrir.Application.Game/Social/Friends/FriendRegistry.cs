namespace Fenrir.Application.Game.Social.Friends;

/// <summary>
///     Soft outcomes of CZ_FRIEND_ASK_SEND -- mirrors ZC_FRIEND_ANSWER_RECV's pre-check codes
///     (contracts/05_social.md).
/// </summary>
public enum FriendAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide friend-request negotiation authority (CZ_FRIEND_* family). The actual 10-slot friend
///     list is durable, per-character state (game.CharacterFriends, cached on
///     <c>PlayerRuntimeState.Friends</c>), mutated directly by <c>FriendAddHandler</c>/
///     <c>FriendRemoveHandler</c> -- this registry only tracks the ephemeral ask/answer handshake, same
///     shape as <c>Party.PartyRegistry</c>. Unlike party accept (which joins immediately), a friend
///     accept only unlocks each side to separately send its own CZ_FRIEND_MAKE_SEND at its own pace --
///     <see cref="_acceptedFor" /> survives past the answer for that reason, cleared on that character's
///     own successful add (open issue: "état 3" semantics weren't fully re-derived from source for this
///     corner).
/// </summary>
public sealed class FriendRegistry
{
    /// <summary>characterId -&gt; the OTHER character it may now add via its own FriendMake (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

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

    /// <summary>
    ///     CZ_FRIEND_ANSWER_SEND. On accept, BOTH sides become eligible to call their own FriendMake (
    ///     <see cref="TryConsumeAccepted" />).
    /// </summary>
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
    ///     CZ_FRIEND_MAKE_SEND's precondition ("état 3") -- its wire payload carries only a slot INDEX,
    ///     no name/id, so "other" is whoever <paramref name="characterId" /> most recently accepted (one
    ///     at a time in this model). Consumed on success.
    /// </summary>
    public bool TryConsumeAccepted(int characterId, out int otherId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(characterId, out otherId);
        }
    }
}
