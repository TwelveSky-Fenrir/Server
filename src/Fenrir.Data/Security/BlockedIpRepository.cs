using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record BlockedIpRepository(ICaeriusNetDbContext Db) : IBlockedIpRepository
{
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Exists", 1, CommandTimeoutSeconds)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.FirstQueryAsync<BlockedIpRowDto>(sp, ct) is not null;
    }

    public async ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Add", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> RemoveAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Remove", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
