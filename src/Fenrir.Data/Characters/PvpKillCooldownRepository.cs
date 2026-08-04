using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record PvpKillCooldownRepository(ICaeriusNetDbContext Db) : IPvpKillCooldownRepository
{
    public async ValueTask<PvpKillCooldownClaimResultDto> TryClaimAsync(
        Guid claimId,
        int attackerCharacterId,
        int defenderCharacterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_PvpKillCooldown_TryClaim", 1)
            .AddParameter("ClaimId", claimId, SqlDbType.UniqueIdentifier)
            .AddParameter("AttackerCharacterId", attackerCharacterId, SqlDbType.Int)
            .AddParameter("DefenderCharacterId", defenderCharacterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<PvpKillCooldownClaimResultDto>(sp, ct).ConfigureAwait(false) ??
               throw new InvalidOperationException(
                   "usp_PvpKillCooldown_TryClaim must return a definitive cooldown-claim result.");
    }
}
