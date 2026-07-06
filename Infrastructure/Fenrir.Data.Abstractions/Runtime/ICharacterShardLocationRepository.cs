namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Cross-shard character-location directory (<c>runtime.CharacterShardLocation</c>) -- a same-shard-miss
///     fallback for whisper/friend-locate/guild-find lookups (see <c>WhisperService</c>/<c>FriendService</c>/
///     <c>FindGuildMemberService</c>), never the primary lookup path. Written once per world-entry
///     (<see cref="UpsertAsync" />) and once per true disconnect (<see cref="RemoveAsync" />) -- never on a
///     per-tick or intra-shard zone-hop path. <c>MapId</c> staleness for a character who has since moved to
///     another map on the SAME shard is an accepted bound; the always-fresh path for that is
///     <c>ZoneRegistry</c>. Every read filters out shards whose <c>runtime.GameServerDirectory</c> heartbeat
///     has gone stale (see the stored procedures), so a crashed shard's rows silently stop being offered
///     with no separate cleanup job.
/// </summary>
public interface ICharacterShardLocationRepository
{
    public ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
        CancellationToken ct);

    /// <summary>
    ///     Scoped to <paramref name="shardId" /> too, not just <paramref name="characterId" />: a stale remove
    ///     from a shard the character already reconnected away from can never clobber the fresh row the new
    ///     shard's own <see cref="UpsertAsync" /> just wrote.
    /// </summary>
    public ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct);

    public ValueTask<CharacterShardLocationDto?> FindByNameAsync(string avatarName, CancellationToken ct);

    public ValueTask<CharacterShardLocationDto?> FindByCharacterIdAsync(int characterId, CancellationToken ct);
}
