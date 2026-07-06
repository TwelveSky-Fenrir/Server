using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IFriendRepository, used only for RenameAvatarService's friend-list refusal
///     check -- every member besides <see cref="GetByCharacterAsync" /> throws, since nothing else on this
///     path calls them.
/// </summary>
internal sealed class FakeFriendRepository : IFriendRepository
{
    private readonly Dictionary<int, List<CharacterFriendDto>> _friendsByCharacterId = new();

    /// <summary>Every characterId GetByCharacterAsync was called with, in call order -- proves ordering/short-circuit.</summary>
    public List<int> QueriedCharacterIds { get; } = [];

    /// <summary>No character has any friends at all.</summary>
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
}
