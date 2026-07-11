using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeMentorRepository : IMentorRepository
{
    private readonly Dictionary<int, CharacterMentorDto> _mentorByCharacterId = new();

    public List<int> QueriedCharacterIds { get; } = [];

    public ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct)
    {
        QueriedCharacterIds.Add(characterId);
        return ValueTask.FromResult(_mentorByCharacterId.GetValueOrDefault(characterId));
    }

    public ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public static FakeMentorRepository Empty()
    {
        return new FakeMentorRepository();
    }

    public static FakeMentorRepository With(int characterId, CharacterMentorDto mentor)
    {
        var repository = new FakeMentorRepository();
        repository._mentorByCharacterId[characterId] = mentor;
        return repository;
    }
}
