using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeFriendRepository : IFriendRepository
{
    private readonly Dictionary<int, List<CharacterFriendDto>> _friendsByCharacterId = new();

    public List<int> QueriedCharacterIds { get; } = [];

    public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
        CancellationToken ct)
    {
        QueriedCharacterIds.Add(characterId);
        var friends = _friendsByCharacterId.TryGetValue(characterId, out var rows) ? rows : [];
        return ValueTask.FromResult(new ReadOnlyCollection<CharacterFriendDto>(friends));
    }

    public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public static FakeFriendRepository Empty()
    {
        return new FakeFriendRepository();
    }

    public static FakeFriendRepository With(int characterId, params CharacterFriendDto[] friendRows)
    {
        var repository = new FakeFriendRepository();
        repository._friendsByCharacterId[characterId] = [.. friendRows];
        return repository;
    }
}
