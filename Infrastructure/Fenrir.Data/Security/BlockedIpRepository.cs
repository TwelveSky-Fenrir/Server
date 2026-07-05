using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Security;

public sealed record BlockedIpRepository(ICaeriusNetDbContext Db) : IBlockedIpRepository
{
    public async ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Exists", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<BlockedIpRowDto>(sp, ct);
        return rows.Count > 0;
    }
}
