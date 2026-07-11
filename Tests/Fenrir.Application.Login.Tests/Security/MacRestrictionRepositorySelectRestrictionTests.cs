using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Tests.Security;

public class MacRestrictionRepositorySelectRestrictionTests
{
    [Fact]
    public void SelectRestriction_MachineGuidMatchesButMacAddressDiffers_StillMatches()
    {
        var rows = ImmutableArray.Create(
            new MacRestrictionRowDto(1, "AA-AA-AA-AA-AA-AA", "shared-adapter-guid", 0));

        var match = MacRestrictionRepository.SelectRestriction(rows, "BB-BB-BB-BB-BB-BB", "shared-adapter-guid");

        Assert.NotNull(match);
        Assert.Equal(0, match!.AccountLimit);
    }

    [Fact]
    public void SelectRestriction_MachineGuidMatch_TakesPrecedenceOverAnUnrelatedMacWideDefault()
    {
        var macWideDefault = new MacRestrictionRowDto(1, "AA-AA-AA-AA-AA-AA", null, 0);
        var guidOverride = new MacRestrictionRowDto(2, "CC-CC-CC-CC-CC-CC", "trusted-guid", 5);
        var rows = ImmutableArray.Create(macWideDefault, guidOverride);

        var match = MacRestrictionRepository.SelectRestriction(rows, "DD-DD-DD-DD-DD-DD", "trusted-guid");

        Assert.NotNull(match);
        Assert.Equal(5, match!.AccountLimit);
    }

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
