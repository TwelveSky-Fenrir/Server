using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Social;

public interface IFriendRepository
{
    public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct);

    public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct);

    public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct);
}
