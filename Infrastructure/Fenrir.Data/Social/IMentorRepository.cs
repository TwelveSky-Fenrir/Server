namespace Fenrir.Data.Social;

/// <summary>Abstraction over Fenrir.Data.Social.MentorRepository for DI/testability.</summary>
public interface IMentorRepository
{
    public ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct);

    public ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct);
}
