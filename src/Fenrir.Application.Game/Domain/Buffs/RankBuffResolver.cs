namespace Fenrir.Application.Game.Domain.Buffs;

public static class RankBuffResolver
{
    public enum Outcome
    {
        Rejected,

        WorldBattleActive,

        Success
    }

    private static readonly int[] RequiredStoneCount = [1, 2, 2, 3, 3, 4, 4];

    public static Result Resolve(int sort, int stoneCount, bool isMovingZone, bool worldBattleActive)
    {
        if (isMovingZone)
            return new Result(Outcome.Rejected);

        if (worldBattleActive)
            return new Result(Outcome.WorldBattleActive);

        if (sort is < 1 or > 7)
            return new Result(Outcome.Rejected);

        return stoneCount < RequiredStoneCount[sort - 1]
            ? new Result(Outcome.Rejected)
            : new Result(Outcome.Success);
    }

    public readonly record struct Result(Outcome Outcome)
    {
        public bool Succeeded => Outcome == Outcome.Success;
        public bool SilentlyIgnored => Outcome == Outcome.WorldBattleActive;
    }
}
