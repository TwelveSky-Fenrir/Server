using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record MacRestrictionRepository(ICaeriusNetDbContext Db) : IMacRestrictionRepository
{
    public async ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        var limit = await GetConfiguredAccountLimitAsync(macAddress, machineGuid, ct);
        return limit is <= 0;
    }

    public async ValueTask<int?> GetConfiguredAccountLimitAsync(string macAddress, string? machineGuid,
        CancellationToken ct)
    {
        if (macAddress.Length == 0)
            return null;

        var rows = await GetAllAsync(ct);
        return SelectRestriction(rows, macAddress, machineGuid)?.AccountLimit;
    }

    internal static MacRestrictionRowDto? SelectRestriction(ImmutableArray<MacRestrictionRowDto> rows,
        string macAddress, string? machineGuid)
    {
        MacRestrictionRowDto? macWideDefault = null;

        foreach (var row in rows)
        {
            if (machineGuid is not null && row.MachineGuid == machineGuid)
                return row;

            if (row.MachineGuid is null && row.MacAddress == macAddress)
                macWideDefault = row;
        }

        return macWideDefault;
    }

    private ValueTask<ImmutableArray<MacRestrictionRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_MacRestriction_GetAll")
            .AddInMemoryCache("admin:mac-restrictions", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<MacRestrictionRowDto>(sp, ct);
    }

    public async ValueTask<int> AddAsync(string macAddress, string? machineGuid, int accountLimit,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_MacRestriction_Add", 1)
            .AddParameter("MacAddress", macAddress, SqlDbType.VarChar)
            .AddParameter("MachineGuid", (object?)machineGuid ?? DBNull.Value, SqlDbType.VarChar)
            .AddParameter("AccountLimit", accountLimit, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
