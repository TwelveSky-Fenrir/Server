using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record MonsterMoneyGrantRepository(ICaeriusNetDbContext Db) : IMonsterMoneyGrantRepository
{
    public async ValueTask<MonsterMoneyGrantResultDto> ApplyIdempotentAsync(
        Guid correlationId,
        int characterId,
        long amount,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_MonsterMoneyGrant_ApplyIdempotent", 1)
            .AddParameter("CorrelationId", correlationId, SqlDbType.UniqueIdentifier)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.BigInt)
            .Build();

        return await Db.FirstQueryAsync<MonsterMoneyGrantResultDto>(sp, ct).ConfigureAwait(false) ??
               throw new InvalidOperationException(
                   "usp_MonsterMoneyGrant_ApplyIdempotent must return an applied or already-applied result.");
    }
}
