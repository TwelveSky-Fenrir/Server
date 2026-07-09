using Fenrir.Application.Game.Domain.Social;

namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>Soft outcomes of CZ_GUILD_ASK_SEND -- mirrors ZC_GUILD_ANSWER_RECV's pre-check codes.</summary>
public enum GuildInviteAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide guild-invitation negotiation authority. Mirrors the legacy's <c>mGuildProcessState</c> machine
///     (1=asker waiting, 2=target waiting, 3=accepted), with <see cref="_acceptedFor" /> surviving past the answer.
/// </summary>
public sealed class GuildInviteRegistry
{
    /// <summary>askerId -&gt; the target it may now finalize the join for (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    /// <summary>
    ///     WS1.4: this family's own cross-shard ASK-PUBLISH-ONLY half, folded into <see cref="IsNegotiating" />
    ///     -- see <see cref="CrossShardNegotiationTracker" />'s own remarks. Unlike Party/Friend, no
    ///     <c>ISocialCrossShardRelayHandler</c> is registered for <c>SocialCrossShardRelayKind.GuildInvite</c>
    ///     yet (see <c>GuildInviteService.AskAsync</c>'s own remarks for why), so only the OUTBOUND half of
    ///     this tracker is ever populated -- an inbound cross-shard guild invite can never be delivered here
    ///     today. <see cref="TryCancel" /> still consumes an outbound entry so an asker who published a
    ///     cross-shard invite that will never be answered is not stuck busy forever.
    /// </summary>
    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>
    ///     Guild-family half of the legacy <c>CheckCommunityWork</c> exclusivity check (pending ask, either
    ///     direction). Public so sibling negotiation families (e.g. Trade ask, see <c>TradeInviteService</c>)
    ///     can compose a cross-family busy check without duplicating this registry's own state -- the same
    ///     reason <see cref="Fenrir.Application.Game.Domain.Social.Duel.DuelRegistry.IsNegotiating" />/
    ///     <see cref="Fenrir.Application.Game.Domain.Social.Trade.TradeRegistry.IsBusy" /> are public. Also
    ///     includes a still-open outbound cross-shard invite.
    /// </summary>
    public bool IsNegotiating(int characterId)
    {
        return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
               _crossShard.IsPending(characterId);
    }

    /// <summary>
    ///     WS1.4 asker-side cross-shard invite registration (ASK-PUBLISH-ONLY -- see <see cref="_crossShard" />'s
    ///     own remarks). Same busy gate as <see cref="TryAsk" />. Unlike <see cref="TryAsk" />, this method
    ///     takes its own lock (mirroring the sibling registries' own <c>TryAskCrossShard</c> methods) since,
    ///     unlike <see cref="IsNegotiating" /> above, it is not already called from within a lock by any
    ///     existing caller.
    /// </summary>
    public GuildInviteAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return GuildInviteAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask)
                ? GuildInviteAskOutcome.Sent
                : GuildInviteAskOutcome.AskerBusy;
        }
    }

    /// <summary>Caller has already verified the asker's own role/guild membership and the tribe match.</summary>
    public GuildInviteAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return GuildInviteAskOutcome.AskerBusy;
            if (IsNegotiating(targetId))
                return GuildInviteAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return GuildInviteAskOutcome.Sent;
        }
    }

    /// <summary>Withdraws the caller's own still-pending ask -- silent no-op if not currently pending.</summary>
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

    /// <summary>
    ///     Accept promotes both sides to legacy state 3 and remembers the acceptance for the asker's later finalize;
    ///     refuse resets both to state 0.
    /// </summary>
    public bool TryAnswer(int targetId, bool accepted, out int askerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            _pendingByAsker.Remove(askerId);

            if (accepted)
                _acceptedFor[askerId] = targetId;

            return true;
        }
    }

    /// <summary>
    ///     The asker consumes this, not the target -- the finalize request's payload carries no name/id, so the target is
    ///     whoever most recently accepted.
    /// </summary>
    public bool TryConsumeAccepted(int askerId, out int targetId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(askerId, out targetId);
        }
    }
}
