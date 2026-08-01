namespace Fenrir.Application.Game.Domain.Stats;

public static class StatAllocationResolver
{
    public enum BaseStat
    {
        Strength,
        Dexterity,
        Vitality,
        Intelligence
    }

    public enum Outcome
    {
        Disconnect,

        Success
    }

    public const int SmallFixedCategoryMin = 1;

    public const int SmallFixedCategoryMax = 4;

    public const int LargeFixedCategoryMin = 5;

    public const int LargeFixedCategoryMax = 8;

    public const int VariableCategoryMin = 9;

    public const int VariableCategoryMax = 12;

    public static Result Resolve(int statSort, int addValue, int currentStatPoints)
    {
        return statSort switch
        {
            1 => ResolveFixed(BaseStat.Strength, 1, currentStatPoints),
            2 => ResolveFixed(BaseStat.Dexterity, 1, currentStatPoints),
            3 => ResolveFixed(BaseStat.Vitality, 1, currentStatPoints),
            4 => ResolveFixed(BaseStat.Intelligence, 1, currentStatPoints),
            5 => ResolveFixed(BaseStat.Strength, 5, currentStatPoints),
            6 => ResolveFixed(BaseStat.Dexterity, 5, currentStatPoints),
            7 => ResolveFixed(BaseStat.Vitality, 5, currentStatPoints),
            8 => ResolveFixed(BaseStat.Intelligence, 5, currentStatPoints),
            9 => ResolveVariable(BaseStat.Strength, addValue, currentStatPoints),
            10 => ResolveVariable(BaseStat.Dexterity, addValue, currentStatPoints),
            11 => ResolveVariable(BaseStat.Vitality, addValue, currentStatPoints),
            12 => ResolveVariable(BaseStat.Intelligence, addValue, currentStatPoints),
            _ => Result.Disconnect
        };
    }

    private static Result ResolveFixed(BaseStat stat, int amount, int currentStatPoints)
    {
        return currentStatPoints < amount
            ? Result.Disconnect
            : new Result(Outcome.Success, stat, amount, currentStatPoints - amount);
    }

    private static Result ResolveVariable(BaseStat stat, int addValue, int currentStatPoints)
    {
        return addValue < 1 || addValue > currentStatPoints
            ? Result.Disconnect
            : new Result(Outcome.Success, stat, addValue, currentStatPoints - addValue);
    }

    public readonly record struct Result(Outcome Outcome, BaseStat Stat, int Amount, int NewStatPoints)
    {
        public bool Succeeded => Outcome == Outcome.Success;

        public static Result Disconnect { get; } = new(Outcome.Disconnect, default, 0, 0);
    }
}
