using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Characters;

public interface ICharacterLogoutStateRepository
{
    public ValueTask UpsertAsync(int characterId, int lastZone, int posX, int posY, int posZ, int life, int mana,
        CancellationToken ct);

    public ValueTask<ImmutableArray<CharacterLogoutStateDto>> GetByAccountAsync(int accountId, CancellationToken ct);
}
