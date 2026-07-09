namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard social-negotiation relay outbox -- the six
///     negotiation *.Services call sites (<c>PartyInviteService.Invite</c>, <c>FriendService.Ask</c>,
///     <c>MentorAskService.Ask</c>, <c>DuelService.Ask</c>, <c>TradeInviteService.Invite</c>,
///     <c>GuildInviteService.Ask</c>, plus each one's own answer path) only need this narrow surface, not
///     the concrete <c>SocialCrossShardRelayHost</c>'s own lifecycle. Same "non-generic handle for a hot
///     synchronous call site" shape as <see cref="IGuildTribeBroadcastRelayQueue" /> -- see that interface's
///     own remarks for why <c>*.Services</c> depends on <c>Fenrir.Data.Abstractions</c> only rather than the
///     concrete Hosting project directly.
/// </summary>
public interface ISocialCrossShardRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue of either an Ask (after the asker-side local pre-checks that do
    ///     not need the target's live state) or an Answer (after a human responds to a cross-shard prompt,
    ///     addressed back to the Ask's own <see cref="SocialCrossShardRelayEntry.SourceShardId" /> via
    ///     <see cref="SocialCrossShardRelayEntry.AskRelayId" />). Never awaits, never throws on backpressure.
    ///     Returns false only if the bounded channel is full; the cross-shard leg of this one negotiation
    ///     step is then silently dropped.
    /// </summary>
    public bool Enqueue(SocialCrossShardRelayEntry entry);
}
