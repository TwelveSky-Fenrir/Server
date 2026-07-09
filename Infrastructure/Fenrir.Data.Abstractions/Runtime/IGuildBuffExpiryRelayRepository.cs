using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.GuildBuffExpiryRelay</c>/<c>runtime.GuildBuffExpiryCursor</c> --
///     the cross-shard fan-out mechanism for the guild-buff-reserve-exhaustion immediate strip-effect push
///     (see <see cref="GuildBuffExpiryRelayEntry" />'s own remarks for the full design). Consumed exclusively
///     by <c>GuildBuffExpiryRelayHost</c>'s own drain/poll loop -- never called directly from
///     <c>Hosting.Guilds.GuildBuffDecayHost</c>'s own detection pass (see <see cref="IGuildBuffExpiryRelayQueue" />
///     for that boundary).
/// </summary>
public interface IGuildBuffExpiryRelayRepository
{
    /// <summary>
    ///     <c>usp_GuildBuffExpiryRelay_Publish</c>: appends one row for every OTHER live shard to pick up on
    ///     its own next poll.
    /// </summary>
    public ValueTask PublishAsync(GuildBuffExpiryRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_GuildBuffExpiryRelay_Poll</c>: returns every row published by some other shard since this
    ///     shard's own last poll, advances this shard's own cursor past them, and (as a side effect) reaps
    ///     rows older than <paramref name="retentionSeconds" /> regardless of which shard published them.
    /// </summary>
    public ValueTask<ImmutableArray<GuildBuffExpiryRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
