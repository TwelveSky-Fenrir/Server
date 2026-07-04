using System.Collections.ObjectModel;

namespace Fenrir.Data.Social;

/// <summary>Abstraction over Fenrir.Data.Social.FriendRepository for DI/testability.</summary>
public interface IFriendRepository
{
    public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct);

    public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct);

    public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct);
}
