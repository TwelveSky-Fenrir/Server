using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="ICharacterLogoutStateRepository" /> -- records every capture (upsert)
///     so a test can assert the D1 logout-info snapshot was written after a successful world entry, and returns
///     whatever rows are staged for <see cref="GetByAccountAsync" />. A capture failure never aborts a
///     successful entry (EnterWorldService isolates it), so the default no-op recording behavior is sufficient
///     for the enter-world tests, which only need it to not throw.
/// </summary>
internal sealed class FakeCharacterLogoutStateRepository : ICharacterLogoutStateRepository
{
    public List<CharacterLogoutStateSnapshot> Captured { get; } = [];

    public List<CharacterLogoutStateDto> ByAccount { get; set; } = [];

    public ValueTask UpsertAsync(int characterId, int lastZone, int posX, int posY, int posZ, int life, int mana,
        CancellationToken ct)
    {
        Captured.Add(new CharacterLogoutStateSnapshot(characterId, lastZone, posX, posY, posZ, life, mana));
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<CharacterLogoutStateDto>> GetByAccountAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(ByAccount.ToImmutableArray());
    }
}
