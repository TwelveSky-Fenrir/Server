using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IGameServerDirectoryRepository
{
    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct);

    /// <summary>Filters on <see cref="GameServerDirectoryDefaults.StalenessCutoffSeconds" /> -- see its own remarks.</summary>
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct);

    /// <summary>
    ///     Same read as <see cref="GetDirectoryAsync(CancellationToken)" />, but with an explicit staleness
    ///     cutoff instead of the <see cref="GameServerDirectoryDefaults.StalenessCutoffSeconds" /> default --
    ///     lets a caller that knows its own configured heartbeat cadence widen (or narrow) the window instead
    ///     of being locked to the shipped default (<c>usp_GameServer_GetDirectory</c>'s
    ///     <c>@StalenessCutoffSeconds</c> parameter).
    /// </summary>
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(int stalenessCutoffSeconds,
        CancellationToken ct);

    /// <summary>
    ///     Proactively evicts <paramref name="shardId" />'s row (and the default-cutoff
    ///     <see cref="GetDirectoryAsync(CancellationToken)" /> read cache) the moment a caller detects the
    ///     shard is actually dead -- e.g. a zone-transfer reachability probe that failed to connect -- instead
    ///     of waiting up to ~17s for the row's own freshness window plus cache TTL to age it out on their own.
    ///     A shard already gone (aged out, or evicted by a concurrent caller racing the same detection) is a
    ///     silent no-op, never an error.
    /// </summary>
    public ValueTask MarkUnreachableAsync(byte shardId, CancellationToken ct);
}
