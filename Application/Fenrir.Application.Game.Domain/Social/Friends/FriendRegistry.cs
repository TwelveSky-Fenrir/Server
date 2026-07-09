using Fenrir.Application.Game.Domain.Social;

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

    /// <summary>
    ///     WS1.4: this family's own cross-shard ask/answer half, folded into <see cref="IsNegotiating" /> --
    ///     see <see cref="CrossShardNegotiationTracker" />'s own remarks.
    /// </summary>
    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>
    ///     Friend-family half of the legacy <c>CheckCommunityWork</c> exclusivity check. Public so sibling
    ///     negotiation families (e.g. Guild ask, see <c>GuildInviteService</c>) can compose a cross-family busy
    ///     check without duplicating this registry's own state. Includes <see cref="_acceptedFor" /> (state 3,
    ///     mutually accepted and awaiting the accepting side's own CZ_FRIEND_MAKE_SEND commit) -- mirrors
    ///     <c>MentorRegistry.IsNegotiating</c>'s own inclusion of <c>_acceptedByMaster</c>. Also includes a
    ///     still-open cross-shard ask, either side (<see cref="CrossShardNegotiationTracker.IsPending" />).
    /// </summary>
    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedFor.ContainsKey(characterId) || _acceptedFor.ContainsValue(characterId) ||
                   _crossShard.IsPending(characterId);
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

    /// <summary>
    ///     WS1.4 asker-side cross-shard ask registration -- same busy gate as <see cref="TryAsk" />, but the
    ///     target is not locally resolvable (it lives on <paramref name="ask" />'s own
    ///     <see cref="CrossShardOutboundAsk.TargetShardId" />), so there is no local <c>_pendingByTarget</c>
    ///     entry to mirror it against.
    /// </summary>
    public FriendAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return FriendAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask) ? FriendAskOutcome.Sent : FriendAskOutcome.AskerBusy;
        }
    }

    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(askerId, out targetId))
            {
                _pendingByTarget.Remove(targetId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(askerId, out var crossShardAsk))
            {
                targetId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    /// <summary>WS1.4 target-side: registers a delivered-in cross-shard ask once local pre-checks pass.</summary>
    public bool TryRegisterCrossShardInbound(int targetId, CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(targetId))
                return false;

            return _crossShard.TryRegisterInbound(targetId, ask);
        }
    }

    /// <summary>
    ///     WS1.4 target-side: consumes a delivered-in cross-shard ask when the local target answers. False if
    ///     <paramref name="targetId" /> has no cross-shard ask pending -- the caller falls back to
    ///     <see cref="TryAnswer" /> in that case.
    /// </summary>
    public bool TryConsumeCrossShardInbound(int targetId, out CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeInbound(targetId, out ask);
        }
    }

    /// <summary>
    ///     WS1.4: consumes this shard's own held outbound cross-shard ask once the matching Answer is
    ///     delivered back. False if nothing was pending for <paramref name="askerId" /> (already cancelled,
    ///     or a stale/duplicate Answer).
    /// </summary>
    public bool TryConsumeCrossShardOutbound(int askerId, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeOutbound(askerId, out ask);
        }
    }

    /// <summary>
    ///     WS1.4: sets ONE side of the mutual accept state (mirrors one half of <see cref="TryAnswer" />'s own
    ///     accept branch) -- used by the cross-shard completion paths where the two sides live in different
    ///     <see cref="FriendRegistry" /> instances (different shard processes) and must each set their own
    ///     local half independently, unlike the same-shard <see cref="TryAnswer" /> which sets both atomically
    ///     under one lock because both characters share this same registry instance.
    /// </summary>
    public void MarkAccepted(int characterId, int otherCharacterId)
    {
        lock (_lock)
        {
            _acceptedFor[characterId] = otherCharacterId;
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
