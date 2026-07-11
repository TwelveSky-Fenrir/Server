using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Crafting;

public static class LegendaryPetCraftResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly Result Rejected = new(Outcome.Rejected, 0);

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
