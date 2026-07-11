using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record MacRestrictionRepository(ICaeriusNetDbContext Db) : IMacRestrictionRepository
{
    public async ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        if (macAddress.Length == 0)
            return false;

        var rows = await GetAllAsync(ct);
        var match = SelectRestriction(rows, macAddress, machineGuid);
        return match is not null && match.AccountLimit <= 0;
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
}
