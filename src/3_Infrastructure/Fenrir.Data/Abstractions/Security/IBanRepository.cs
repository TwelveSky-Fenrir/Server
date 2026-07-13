namespace Fenrir.Data.Abstractions.Security;

public interface IBanRepository
{
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct);

    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null);
}
