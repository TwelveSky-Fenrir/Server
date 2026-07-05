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

    /// <summary>
    ///     Exact (MacAddress, MachineGuid) override wins over the MAC-wide default row (MachineGuid IS NULL),
    ///     matching UQ_MacRestrictions_MacAddress_MachineGuid's two-tier design. Covered via IsBannedAsync's own
    ///     integration tests (MacRestrictionProcTests), not in isolation.
    /// </summary>
    private static MacRestrictionRowDto? SelectRestriction(ImmutableArray<MacRestrictionRowDto> rows,
        string macAddress, string? machineGuid)
    {
        MacRestrictionRowDto? macWideDefault = null;

        foreach (var row in rows)
        {
            if (row.MacAddress != macAddress)
                continue;

            if (machineGuid is not null && row.MachineGuid == machineGuid)
                return row;

            if (row.MachineGuid is null)
                macWideDefault = row;
        }

        return macWideDefault;
    }

    /// <summary>Short in-memory cache, loaded whole and matched client-side (legacy loaded this once at boot too).</summary>
    private ValueTask<ImmutableArray<MacRestrictionRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_MacRestriction_GetAll", 16)
            .AddInMemoryCache("admin:mac-restrictions", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<MacRestrictionRowDto>(sp, ct);
    }
}
