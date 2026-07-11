using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record TribeFourQuotaRepository(ICaeriusNetDbContext Db) : ITribeFourQuotaRepository
{
    public async ValueTask<bool> TryConsumeAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_TribeFourQuota_TryConsume", 1).Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }
}
