using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IMentorRepository, used only for RenameAvatarService's teacher-bond/
///     student-bond refusal checks -- every member besides <see cref="GetForCharacterAsync" /> throws, since
///     nothing else on this path calls them.
/// </summary>
internal sealed class FakeMentorRepository : IMentorRepository
{
    private readonly Dictionary<int, CharacterMentorDto> _mentorByCharacterId = new();

    /// <summary>Every characterId GetForCharacterAsync was called with, in call order -- proves ordering/short-circuit.</summary>
    public List<int> QueriedCharacterIds { get; } = [];

    /// <summary>No character has any teacher/student bond at all.</summary>
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
}
