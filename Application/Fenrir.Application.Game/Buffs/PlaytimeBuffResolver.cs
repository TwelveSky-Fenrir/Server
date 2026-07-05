namespace Fenrir.Application.Game.Buffs;

/// <summary>
///     Pure resolver for CZ_TIME_EFFECT_SEND (op97, S04_MyWork02.cpp:12989). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Legacy quirk, reproduced not fixed (D8): <c>wAvatar.aPlayTime2</c> is unconditionally hard-reset to 300 at
///     the top of the handler on every call, before the threshold check runs. Combined with the EU33
///     (non-HUNGAME) <c>tEffectTime</c> table (120/180/240/300/360, S04_MyWork02.cpp:509), the threshold
///     <c>aPlayTime2 &gt;= tEffectTime[sort-1] - 60</c> reduces to <c>300 &gt;= tEffectTime[sort-1] - 60</c>, which
///     is true for every sort in [1,5] -- so every valid sort always succeeds. An out-of-range sort is silently
///     ignored (matches the legacy: no default case, no Quit()).
/// </remarks>
public static class PlaytimeBuffResolver
{
    /// <summary>tEffectTime, EU33 (non-HUNGAME) build.</summary>
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
