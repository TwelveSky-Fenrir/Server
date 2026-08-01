namespace Fenrir.Application.Game.Domain.Buffs;

public static class PlaytimeBuffResolver
{
    public const int PlayTimeClobberValue = 300;

    private static readonly int[] EffectTime = [120, 180, 240, 300, 360];

    public static Result Resolve(int sort)
    {
        if (sort is < 1 or > 5)
            return Result.NoOp;

        var effectTime = EffectTime[sort - 1];
        return new Result(true, effectTime, effectTime);
    }

    public readonly record struct Result(bool Applied, int Value, int NewStateTimeEffect)
    {
        public static readonly Result NoOp = new(false, -1, 0);
    }
}
