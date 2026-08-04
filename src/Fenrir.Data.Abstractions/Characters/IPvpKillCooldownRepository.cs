namespace Fenrir.Data.Abstractions.Characters;

public interface IPvpKillCooldownRepository
{
    public ValueTask<PvpKillCooldownClaimResultDto> TryClaimAsync(
        Guid claimId,
        int attackerCharacterId,
        int defenderCharacterId,
        CancellationToken ct);
}
