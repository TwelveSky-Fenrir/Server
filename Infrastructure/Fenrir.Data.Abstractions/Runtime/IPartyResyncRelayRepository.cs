using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.PartyResyncRelay</c>/<c>runtime.PartyResyncRelayCursor</c> -- the
///     cross-shard transport for the party-membership resync-on-reconnect flow (legacy RELAY_LOGIN_PARTY_CHECK,
///     Server/Header/Protocol/STRUCT.h:1093-1098; hub reconciliation Server/ts25center/S04_MyWork02.cpp:
///     1443-1494). Fan-out, exactly like its sibling <see cref="IGuildTribeBroadcastRelayRepository" /> --
///     <see cref="PollAsync" /> returns rows published by every OTHER shard (never the caller's own). Intended
///     to be consumed exclusively by <c>PartyResyncRelayHost</c>'s own drain/poll loop -- never called directly
///     from an <c>IAsyncPacketHandler</c>'s per-connection path.
/// </summary>
public interface IPartyResyncRelayRepository
{
    /// <summary>
    ///     <c>usp_PartyResyncRelay_Publish</c>: appends one party-resync row for every OTHER live shard's own
    ///     next poll to pick up.
    /// </summary>
    public ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_PartyResyncRelay_Poll</c>: returns every party-resync row some OTHER shard published since
    ///     <paramref name="shardId" />'s own last poll, advances that shard's own cursor past them, and (as a
    ///     side effect) reaps rows older than <paramref name="retentionSeconds" /> regardless of source shard.
    /// </summary>
    public ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
