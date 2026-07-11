using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeRuneRepository : IRuneRepository
{

        public IReadOnlyList<CharacterRuneSocketDto> RowsToReturn { get; set; } = [];

        public int? LastPersistedCharacterId { get; private set; }

        public IReadOnlyList<CharacterRuneSocketTvp>? LastPersistedRunes { get; private set; }

        public byte? LastPersistedContainer { get; private set; }

        public IReadOnlyList<CharacterItemSlotTvp>? LastPersistedItems { get; private set; }

    public ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<CharacterRuneSocketDto>([..RowsToReturn]));
    }

    public ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes, byte? container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        LastPersistedCharacterId = characterId;
        LastPersistedRunes = runes;
        LastPersistedContainer = container;
        LastPersistedItems = items;
        return ValueTask.CompletedTask;
    }
}
