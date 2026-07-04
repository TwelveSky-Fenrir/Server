namespace Fenrir.Data.Social;

public interface IMentorRepository
{
    public ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct);

    public ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct);
}
