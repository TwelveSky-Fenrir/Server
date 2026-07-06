using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="ICharacterShardLocationRepository" /> -- a plain dictionary keyed by
///     CharacterId, mirroring the real table's two lookup paths (by id, by name).
/// </summary>
internal sealed class FakeCharacterShardLocationRepository : ICharacterShardLocationRepository
{
    private readonly Dictionary<int, CharacterShardLocationDto> _byCharacterId = new();

    public List<(int CharacterId, byte ShardId, short MapId, string AvatarName, byte Tribe)> UpsertCalls { get; } = [];
    public List<(int CharacterId, byte ShardId)> RemoveCalls { get; } = [];
    public bool ThrowOnUpsert { get; set; }
    public bool ThrowOnRemove { get; set; }

    /// <summary>Seeds a row as if some other shard already upserted it -- bypasses <see cref="UpsertAsync" /> itself.</summary>
    public void Seed(CharacterShardLocationDto row)
    {
        _byCharacterId[row.CharacterId] = row;
    }

    public ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
        CancellationToken ct)
    {
        if (ThrowOnUpsert)
            throw new InvalidOperationException("Simulated SQL failure.");

        UpsertCalls.Add((characterId, shardId, mapId, avatarName, tribe));
        _byCharacterId[characterId] = new CharacterShardLocationDto(characterId, shardId, mapId, avatarName, tribe,
            DateTime.UtcNow);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct)
    {
        if (ThrowOnRemove)
            throw new InvalidOperationException("Simulated SQL failure.");

        RemoveCalls.Add((characterId, shardId));

        // Scoped to shardId, matching usp_CharacterShardLocation_Remove: a stale remove from a shard the
        // character already reconnected away from must never clobber the fresh row.
        if (_byCharacterId.TryGetValue(characterId, out var row) && row.ShardId == shardId)
            _byCharacterId.Remove(characterId);

        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterShardLocationDto?> FindByNameAsync(string avatarName, CancellationToken ct)
    {
        foreach (var row in _byCharacterId.Values)
            if (string.Equals(row.AvatarName, avatarName, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult<CharacterShardLocationDto?>(row);

        return ValueTask.FromResult<CharacterShardLocationDto?>(null);
    }

    public ValueTask<CharacterShardLocationDto?> FindByCharacterIdAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(_byCharacterId.TryGetValue(characterId, out var row)
            ? row
            : (CharacterShardLocationDto?)null);
    }
}
