using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Characters;

public interface IRuneRepository
{

        public ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId, CancellationToken ct);

        public ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes,
        byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
