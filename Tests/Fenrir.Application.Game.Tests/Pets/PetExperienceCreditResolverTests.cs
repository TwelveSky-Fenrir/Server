using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers <see cref="PetExperienceCreditResolver" /> against <c>PETSYSTEM::ProcessForExperience</c>
///     (GameSystem_07_Pet.cpp:1920-1971).
/// </summary>
public class PetExperienceCreditResolverTests
{
    private static FrozenDictionary<int, ItemDefinition> Items(params (int Id, byte Sort)[] items)
    {
        var dict = new Dictionary<int, ItemDefinition>();
        foreach (var (id, sort) in items)
            dict[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = sort }, []);
        return dict.ToFrozenDictionary();
    }

    [Fact]
    public void Resolve_NoItemEquipped_IsIneligible()
    {
        var result = PetExperienceCreditResolver.Resolve(0, 0, 1, 800, Items());

        Assert.Equal(PetExperienceCreditResult.Ineligible, result);
    }

    [Fact]
    public void Resolve_ItemIdNotLoaded_IsIneligible()
    {
        var result = PetExperienceCreditResolver.Resolve(541, 0, 1, 800, Items());

        Assert.False(result.IsEligible);
    }

    [Fact]
    public void Resolve_ItemNotPetCategory_IsIneligible()
    {
        // 541 is a real pet-family id, but catalogued here as sort 28 (Phoenix amulet), not 22 (IPET).
        var result = PetExperienceCreditResolver.Resolve(541, 0, 1, 800, Items((541, 28)));

        Assert.False(result.IsEligible);
    }

    [Fact]
    public void Resolve_ActivePet_NormalCredit_NoReactivationNoTierCross()
    {
        var items = Items((541, 22));

        var result = PetExperienceCreditResolver.Resolve(541, 1_000, 1, 800, items);

        Assert.True(result.IsEligible);
        Assert.False(result.ReactivationApplied);
        Assert.Equal(1, result.NewActivity);
        Assert.Equal(800, result.CreditedAmount);
        Assert.Equal(1_800, result.NewGrowth);
        Assert.False(result.TierIncreased);
    }

    [Fact]
    public void Resolve_InactivePet_IsReactivatedEvenWhenNothingIsCredited()
    {
        // item at its category cap already -> CreditedAmount stays 0, but reactivation is unconditional.
        var items = Items((541, 22));

        var result = PetExperienceCreditResolver.Resolve(541, 40_000_000, 0, 800, items);

        Assert.True(result.IsEligible);
        Assert.True(result.ReactivationApplied);
        Assert.Equal(1, result.NewActivity);
        Assert.Equal(0, result.CreditedAmount);
        Assert.Equal(40_000_000, result.NewGrowth);
        Assert.False(result.TierIncreased);
    }

    [Fact]
    public void Resolve_CreditCrossesATierBoundary_ReportsTierIncreased()
    {
        var items = Items((541, 22));

        // category 0 cap is 40,000,000 -- 9,000,000 (22.5%, tier 0) + 2,000,000 = 11,000,000 (27.5%, tier 1).
        var result = PetExperienceCreditResolver.Resolve(541, 9_000_000, 1, 2_000_000, items);

        Assert.Equal(2_000_000, result.CreditedAmount);
        Assert.True(result.TierIncreased);
    }

    [Fact]
    public void Resolve_CreditStaysWithinTheSameTier_DoesNotReportTierIncreased()
    {
        var items = Items((541, 22));

        var result = PetExperienceCreditResolver.Resolve(541, 1_000, 1, 500, items);

        Assert.Equal(500, result.CreditedAmount);
        Assert.False(result.TierIncreased);
    }

    [Fact]
    public void Resolve_UnrecognizedCategoryItem_IsEligibleButCreditsNothing()
    {
        // Real pet item (sort 22) whose id has no entry in either category table.
        var items = Items((999_999, 22));

        var result = PetExperienceCreditResolver.Resolve(999_999, 1_000, 1, 800, items);

        Assert.True(result.IsEligible);
        Assert.Equal(0, result.CreditedAmount);
        Assert.Equal(1_000, result.NewGrowth);
        Assert.False(result.TierIncreased);
    }
}
