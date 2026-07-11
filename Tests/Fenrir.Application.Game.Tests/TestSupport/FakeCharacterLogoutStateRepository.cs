using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

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
