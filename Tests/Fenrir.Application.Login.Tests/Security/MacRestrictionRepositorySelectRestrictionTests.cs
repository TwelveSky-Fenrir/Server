using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Tests.Security;

// Regression coverage for MacRestrictionRepository.SelectRestriction's matching semantics: a pure function
// (no database round trip), so it's exercised directly here rather than only through the Docker-backed
// Fenrir.Data.Tests.Security.MacRestrictionProcTests (which covers the same precedence rules end-to-end
// against a real admin.MacRestrictions table). SelectRestriction is internal -- visible here via
// Infrastructure/Fenrir.Data/AssemblyInfo.cs's InternalsVisibleTo("Fenrir.Application.Login.Tests").
public class MacRestrictionRepositorySelectRestrictionTests
{
    // The confirmed audit finding this regression-tests: legacy's ban lookup is keyed solely by mac_guid
    // (the client-declared adapter name/GUID), Server/ts25login/S08_MyDB.cpp:441
    // ("SELECT mac_limit FROM macinfo WHERE mac_guid='%s';") -- it never predicates on the reported
    // MAC-address bytes at all. Before this fix, SelectRestriction required an exact MacAddress match as a
    // precondition before even considering MachineGuid, so a banned device could evade the ban simply by
    // reporting a different MAC address while keeping the same adapter GUID.
    [Fact]
    public void SelectRestriction_MachineGuidMatchesButMacAddressDiffers_StillMatches()
    {
        var rows = ImmutableArray.Create(
            new MacRestrictionRowDto(1, "AA-AA-AA-AA-AA-AA", "shared-adapter-guid", 0));

        var match = MacRestrictionRepository.SelectRestriction(rows, "BB-BB-BB-BB-BB-BB", "shared-adapter-guid");

        Assert.NotNull(match);
        Assert.Equal(0, match!.AccountLimit);
    }

    // A MachineGuid-specific row wins even when the reported MAC address matches neither its own row nor the
    // MAC-wide default row -- proving the match is keyed purely on MachineGuid, exactly like legacy's WHERE
    // clause, not on any (MacAddress, MachineGuid) pairing.
    [Fact]
    public void SelectRestriction_MachineGuidMatch_TakesPrecedenceOverAnUnrelatedMacWideDefault()
    {
        var macWideDefault = new MacRestrictionRowDto(1, "AA-AA-AA-AA-AA-AA", null, 0); // bans every install on AA-...
        var guidOverride = new MacRestrictionRowDto(2, "CC-CC-CC-CC-CC-CC", "trusted-guid", 5);
        var rows = ImmutableArray.Create(macWideDefault, guidOverride);

        var match = MacRestrictionRepository.SelectRestriction(rows, "DD-DD-DD-DD-DD-DD", "trusted-guid");

        Assert.NotNull(match);
        Assert.Equal(5, match!.AccountLimit);
    }

    // The MAC-wide default row (MachineGuid IS NULL) is a Fenrir-only elaboration with no legacy analog --
    // kept as an additive fallback, so it only ever applies to its own MacAddress, never to an unrelated one.
    [Fact]
    public void SelectRestriction_MacWideDefault_OnlyAppliesToItsOwnMacAddress()
    {
        var rows = ImmutableArray.Create(new MacRestrictionRowDto(1, "AA-AA-AA-AA-AA-AA", null, 0));

        var match = MacRestrictionRepository.SelectRestriction(rows, "BB-BB-BB-BB-BB-BB", "some-other-guid");

        Assert.Null(match);
    }

    [Fact]
    public void SelectRestriction_NoRows_ReturnsNull()
    {
        var match = MacRestrictionRepository.SelectRestriction(ImmutableArray<MacRestrictionRowDto>.Empty,
            "AA-AA-AA-AA-AA-AA", "some-guid");

        Assert.Null(match);
    }
}
