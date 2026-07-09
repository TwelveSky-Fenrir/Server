namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Implemented by whichever of the six negotiation *.Services registries owns a given
///     <see cref="SocialCrossShardRelayKind" /> (<c>PartyInviteService</c> for <see cref="SocialCrossShardRelayKind.Party" />,
///     <c>FriendService</c> for <see cref="SocialCrossShardRelayKind.Friend" />, <c>MentorAskService</c> for
///     <see cref="SocialCrossShardRelayKind.Mentor" />, <c>DuelService</c> for
///     <see cref="SocialCrossShardRelayKind.Duel" />, <c>TradeInviteService</c> for
///     <see cref="SocialCrossShardRelayKind.Trade" />, <c>GuildInviteService</c> for
///     <see cref="SocialCrossShardRelayKind.GuildInvite" />) and registered as an
///     <see cref="ISocialCrossShardRelayHandler" /> in DI alongside that registry. <c>SocialCrossShardRelayHost</c>'s
///     own inbound poll routes every delivered row to the one handler whose <see cref="Kind" /> matches the
///     row -- it deliberately does not deliver anything itself (unlike its fan-out sibling
///     <c>GuildTribeBroadcastRelayHost</c>), because local delivery needs each negotiation's own
///     busy/negotiating gate and in-memory pending-entry table, not a generic poll loop. A row for a
///     <see cref="Kind" /> with no registered handler is logged and dropped.
/// </summary>
public interface ISocialCrossShardRelayHandler
{
    /// <summary>Which of the six negotiation flows this handler owns.</summary>
    public SocialCrossShardRelayKind Kind { get; }

    /// <summary>
    ///     Delivers a cross-shard Ask on the TARGET's own shard: resolve the target character in this
    ///     shard's own <c>ZoneRegistry</c>, re-run the target-side pre-checks that could not be run on the
    ///     asker's shard (busy/stunned/dead, Mentor's level gate, etc.), and either enqueue a declined
    ///     Answer via <see cref="ISocialCrossShardRelayQueue" /> immediately (target unreachable or fails a
    ///     pre-check) or surface the real prompt to the target player, holding a pending entry keyed by
    ///     <see cref="SocialCrossShardRelayDto.RelayId" /> so the human answer -- once given -- can be
    ///     matched back to this Ask and published as an Answer row.
    /// </summary>
    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct);

    /// <summary>
    ///     Delivers a cross-shard Answer on the original ASKER's own shard: match
    ///     <see cref="SocialCrossShardRelayDto.AskRelayId" /> against this shard's own held pending entry
    ///     for that Ask and complete exactly like today's same-shard accept/decline path (using
    ///     <see cref="SocialCrossShardRelayDto.Accepted" />/<see cref="SocialCrossShardRelayDto.ReasonCode" />).
    ///     A no-op (logged, not an error) if no pending entry is found -- e.g. the asker already disconnected
    ///     or the pending entry's own timeout already fired locally.
    /// </summary>
    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct);
}
