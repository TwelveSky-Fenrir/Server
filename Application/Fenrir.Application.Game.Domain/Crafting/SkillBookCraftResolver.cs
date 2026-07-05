using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Pure resolver for CZ_MAKE_SKILL_SEND (op30) -- 4 fragment-fusion recipes, S04_MyWork02.cpp:5868-6017. No
///     I/O, no Zone dependency.
/// </summary>
public static class SkillBookCraftResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly Result Rejected = new(Outcome.Rejected, 0);

    /// <summary>Sorts 0-2: fixed 4-fragment recipes, no roll at all.</summary>
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

    /// <summary>
    ///     Sort 3, "War God" -- live in EU33 (__REBIRTH__ active; the dead <c>Quit()</c> lives only in the
    ///     #else arm for builds without it). <paramref name="previousTribe" /> must be in [0,2]: the legacy's
    ///     own lookup table (<c>i8th[3][3]</c>) has no row for tribe 3, so a tribe-3 request is rejected here
    ///     rather than reproducing the legacy's out-of-bounds array read -- a genuine crash bug, not behavior
    ///     worth preserving (D8).
    /// </summary>
    public static Result ResolveWarGod(int material1, int material2, int material3, int material4,
        byte previousTribe, IRandomSource random)
    {
        if (material1 != SkillBookCraftCatalog.WarGodMaterial1ItemId ||
            material2 != SkillBookCraftCatalog.WarGodMaterial2ItemId ||
            material3 != SkillBookCraftCatalog.WarGodMaterial3ItemId ||
            material4 != SkillBookCraftCatalog.WarGodMaterial4ItemId ||
            previousTribe > 2)
            return Rejected;

        // ReturnWarGodSkill: ONE shared draw feeds both r1%5 (20% column0) and r1%2 (else 40/40 split across
        // columns 1/2) -- not two independent rolls.
        var r1 = random.NextInt32(10);
        var column = r1 % 5 == 0 ? 0 : r1 % 2 + 1;
        return new Result(Outcome.Success, SkillBookCraftCatalog.WarGodSkillsByTribe[previousTribe][column]);
    }

    public readonly record struct Result(Outcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
