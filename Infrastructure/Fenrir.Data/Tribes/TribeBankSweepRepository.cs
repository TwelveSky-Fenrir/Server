using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

public sealed record TribeBankSweepRepository(ICaeriusNetDbContext Db) : ITribeBankSweepRepository
{
    public async ValueTask ApplyTaxSweepAsync(long tribe0Amount, long tribe1Amount, long tribe2Amount,
        long tribe3Amount, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_ApplyTaxSweep", 0)
            .AddParameter("Tribe0Amount", tribe0Amount, SqlDbType.BigInt)
            .AddParameter("Tribe1Amount", tribe1Amount, SqlDbType.BigInt)
            .AddParameter("Tribe2Amount", tribe2Amount, SqlDbType.BigInt)
            .AddParameter("Tribe3Amount", tribe3Amount, SqlDbType.BigInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
