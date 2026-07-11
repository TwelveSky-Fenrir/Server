using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IRuneRepository" /> -- <see cref="GetRunesAsync" /> is exercised by
///     EnterWorldService's world-entry hydration, and <see cref="PersistRunesAsync" /> is exercised by
///     RuneSocketService's own atomic rune+inventory persist path (see RuneSocketServiceTests), recording its
///     last call on the <c>LastPersisted*</c> properties below for assertions.
/// </summary>
internal sealed class FakeRuneRepository : IRuneRepository
{
    /// <summary>Scripted return for <see cref="GetRunesAsync" /> -- empty (the default) means "never socketed".</summary>
    public IReadOnlyList<CharacterRuneSocketDto> RowsToReturn { get; set; } = [];

    /// <summary>The <c>characterId</c> from the most recent <see cref="PersistRunesAsync" /> call, if any.</summary>
    public int? LastPersistedCharacterId { get; private set; }

    /// <summary>The whole occupied-socket rune array from the most recent <see cref="PersistRunesAsync" /> call, if any.</summary>
    public IReadOnlyList<CharacterRuneSocketTvp>? LastPersistedRunes { get; private set; }

    /// <summary>The paired inventory container (or <c>null</c> for a rune-only re-anchor) from the most recent call.</summary>
    public byte? LastPersistedContainer { get; private set; }

    /// <summary>The paired inventory container's full post-mutation contents from the most recent call.</summary>
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
