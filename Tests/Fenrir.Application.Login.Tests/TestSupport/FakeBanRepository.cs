using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeBanRepository(bool accountBanned = false) : IBanRepository
{
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(accountBanned);
    }

    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null)
    {
        throw new NotSupportedException();
    }
}
