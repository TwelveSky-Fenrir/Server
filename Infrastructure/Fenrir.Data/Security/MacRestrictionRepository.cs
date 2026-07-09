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
    ///     A MachineGuid match is sufficient on its own, independent of MacAddress, and takes precedence over
    ///     the MAC-wide default row (MachineGuid IS NULL) -- mirrors legacy's ban lookup, which is keyed
    ///     solely by the client-declared adapter GUID string (
    ///     <c>
    ///         SELECT mac_limit FROM macinfo WHERE
    ///         mac_guid='%s';
    ///     </c>
    ///     , Server/ts25login/S08_MyDB.cpp:441) and never predicates on the reported
    ///     MAC-address bytes at all. Requiring an exact MacAddress match as a precondition (as this method
    ///     used to) would let a banned device evade the ban simply by reporting a different MAC address while
    ///     keeping the same adapter GUID. The MAC-wide default row (MachineGuid IS NULL) is a Fenrir-only
    ///     elaboration with no legacy analog, letting an operator ban every adapter GUID seen from one MAC
    ///     address -- kept as an additive fallback since it can only widen ban coverage, never narrow it, so
    ///     it can't reopen the evasion this fixes. Internal (not private) so
    ///     Fenrir.Application.Login.Tests can regression-test the matching semantics directly, without a real
    ///     database (see AssemblyInfo.cs's InternalsVisibleTo). Covered via IsBannedAsync's own integration
    ///     tests too (MacRestrictionProcTests).
    /// </summary>
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

    /// <summary>Short in-memory cache, loaded whole and matched client-side (legacy loaded this once at boot too).</summary>
    private ValueTask<ImmutableArray<MacRestrictionRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_MacRestriction_GetAll")
            .AddInMemoryCache("admin:mac-restrictions", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<MacRestrictionRowDto>(sp, ct);
    }
}
