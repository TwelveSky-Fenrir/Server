namespace Fenrir.Application.Game.Domain.Social.Friends;

/// <summary>Soft outcomes of CZ_FRIEND_ASK_SEND -- mirrors ZC_FRIEND_ANSWER_RECV's pre-check codes.</summary>
public enum FriendAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide friend-request negotiation authority. The actual 10-slot friend list is durable,
///     per-character state (game.CharacterFriends, cached on PlayerRuntimeState.Friends), mutated directly
///     by FriendAddHandler/FriendRemoveHandler -- this registry only tracks the ephemeral ask/answer
///     handshake. Unlike party accept (which joins immediately), a friend accept only unlocks each side to
///     separately send its own CZ_FRIEND_MAKE_SEND at its own pace, so <see cref="_acceptedFor" /> survives
///     past the answer, cleared only on that character's own successful add.
/// </summary>
public sealed class FriendRegistry
{
    /// <summary>characterId -> the other character it may now add via its own FriendMake.</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>
    ///     Friend-family half of the legacy <c>CheckCommunityWork</c> exclusivity check. Public so sibling
    ///     negotiation families (e.g. Guild ask, see <c>GuildInviteService</c>) can compose a cross-family busy
    ///     check without duplicating this registry's own state. Includes <see cref="_acceptedFor" /> (state 3,
    ///     mutually accepted and awaiting the accepting side's own CZ_FRIEND_MAKE_SEND commit) -- mirrors
    ///     <c>MentorRegistry.IsNegotiating</c>'s own inclusion of <c>_acceptedByMaster</c>.
    /// </summary>
    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedFor.ContainsKey(characterId) || _acceptedFor.ContainsValue(characterId);
        }
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
    ///     CZ_FRIEND_MAKE_SEND's precondition -- its wire payload carries only a slot index, no name/id, so
    ///     "other" is whoever characterId most recently accepted (one at a time in this model).
    /// </summary>
    public bool TryConsumeAccepted(int characterId, out int otherId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(characterId, out otherId);
        }
    }
}
