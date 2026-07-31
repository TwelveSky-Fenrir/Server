namespace Fenrir.Data.Abstractions.Runtime;

public interface ICharacterShardLocationRepository
{
    public ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
        CancellationToken ct);

    public ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct);

    public ValueTask<CharacterShardLocationDto?> FindByNameAsync(string avatarName, CancellationToken ct);

    public ValueTask<CharacterShardLocationDto?> FindByCharacterIdAsync(int characterId, CancellationToken ct);
}
