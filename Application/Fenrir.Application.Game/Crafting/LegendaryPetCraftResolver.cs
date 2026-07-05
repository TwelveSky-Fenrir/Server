using Fenrir.Application.Game.Combat;

namespace Fenrir.Application.Game.Crafting;

/// <summary>
///     Pure resolver for CZ_MAKE_ITEM2_SEND tSort==2 (S04_MyWork02.cpp:15074-15141) -- re-rolls an
///     already-Legendary pet into a further-evolved Legendary/Guardian pet for 10,000 CP + 2 catalyst stones.
/// </summary>
public static class LegendaryPetCraftResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly Result Rejected = new(Outcome.Rejected, 0);

    /// <param name="material1Sort">world.Items.Sort of the page1/index1 pet being upgraded; must be 31 or 32.</param>
    /// <param name="material2ItemId">page2/index2 -- catalyst stone (1878 or 2150).</param>
    /// <param name="material3ItemId">page3/index3 -- catalyst stone (1878 or 2150).</param>
    public static Result Resolve(byte material1Sort, int material2ItemId, int material3ItemId,
        int contributionPoints, IRandomSource random)
    {
        if ((material1Sort != LegendaryPetCraftCatalog.Material1RequiredSort1 &&
             material1Sort != LegendaryPetCraftCatalog.Material1RequiredSort2) ||
            !LegendaryPetCraftCatalog.CatalystItemIds.Contains(material2ItemId) ||
            !LegendaryPetCraftCatalog.CatalystItemIds.Contains(material3ItemId) ||
            contributionPoints < LegendaryPetCraftCatalog.ContributionPointCost)
            return Rejected;

        var tierRoll = random.NextInt32(10);
        switch (tierRoll)
        {
            case 0:
            {
                var guardianIndex = random.NextInt32(LegendaryPetCraftCatalog.GuardianPool.Count);
                return new Result(Outcome.Success, LegendaryPetCraftCatalog.GuardianPool[guardianIndex]);
            }
            case < 3:
            {
                var pool2Index = random.NextInt32(LegendaryPetCraftCatalog.LegendaryPool2.Count);
                return new Result(Outcome.Success, LegendaryPetCraftCatalog.LegendaryPool2[pool2Index]);
            }
            default:
            {
                var pool1Index = random.NextInt32(LegendaryPetCraftCatalog.LegendaryPool1.Count);
                return new Result(Outcome.Success, LegendaryPetCraftCatalog.LegendaryPool1[pool1Index]);
            }
        }
    }

    public readonly record struct Result(Outcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
