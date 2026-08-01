using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Crafting;

public static class SkillBookCraftResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly Result Rejected = new(Outcome.Rejected, 0);

    public static Result ResolveFragments(int sort, int material1, int material2, int material3, int material4)
    {
        if (sort < 0 || sort >= SkillBookCraftCatalog.Recipes.Length)
            return Rejected;

        var recipe = SkillBookCraftCatalog.Recipes[sort];
        if (material1 != recipe.Material1 || material2 != recipe.Material2 ||
            material3 != recipe.Material3 || material4 != recipe.Material4)
            return Rejected;

        return new Result(Outcome.Success, recipe.ResultItemId);
    }

    public static Result ResolveWarGod(int material1, int material2, int material3, int material4,
        byte previousTribe, IRandomSource random)
    {
        if (material1 != SkillBookCraftCatalog.WarGodMaterial1ItemId ||
            material2 != SkillBookCraftCatalog.WarGodMaterial2ItemId ||
            material3 != SkillBookCraftCatalog.WarGodMaterial3ItemId ||
            material4 != SkillBookCraftCatalog.WarGodMaterial4ItemId ||
            previousTribe > 2)
            return Rejected;

        var r1 = random.NextInt32(10);
        var column = r1 % 5 == 0 ? 0 : r1 % 2 + 1;
        return new Result(Outcome.Success, SkillBookCraftCatalog.WarGodSkillsByTribe[previousTribe][column]);
    }

    public readonly record struct Result(Outcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
