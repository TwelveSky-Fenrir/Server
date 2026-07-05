using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for IBanRepository: EnterWorldHandler only ever calls IsActiveForCharacterAsync.
internal sealed class FakeBanRepository(bool characterBanned = false) : IBanRepository
{
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(characterBanned);
    }
}
