using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Admin;

public interface IMuteRepository
{
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
        CancellationToken ct);

    public ValueTask<int> CreateAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null);

    public ValueTask LiftAsync(int muteId, CancellationToken ct);
}
