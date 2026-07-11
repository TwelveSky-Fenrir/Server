using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeBanRepository(bool characterBanned = false, int nextBanId = 1) : IBanRepository
{
    public (int? AccountId, int? CharacterId, BanReason Reason, DateTime? ExpiresAtUtc, int? ActorAccountId,
        int? ActorCharacterId)? LastCreatedBan { get; private set; }

    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult(characterBanned);
    }

    public ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null)
    {
        LastCreatedBan = (accountId, characterId, reason, expiresAtUtc, actorAccountId, actorCharacterId);
        return ValueTask.FromResult(nextBanId);
    }
}
