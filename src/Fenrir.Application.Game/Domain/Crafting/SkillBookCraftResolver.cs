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

    public readonly record struct Result(Outcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
