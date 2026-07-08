namespace Fenrir.Application.Game.Domain.Buffs;

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
/// <remarks>
///     Second, independent legacy defect at this same call site (D8, not fixed): because <c>aPlayTime2</c> is
///     also the field <c>S07_MyGame04.cpp:889-896</c>'s tick loop independently accumulates once per real
///     minute (<see cref="World.PlayerRuntimeState.PlayTime2" />, mirrored by
///     <see cref="Simulation.PlayTimeAccrualSystem" />), the same unconditional reset-to-300 described above
///     also permanently clobbers that tick-accumulated progress on every op97 request, valid sort or not. This
///     resolver stays pure and does not itself carry the reset (there is nothing sort-dependent about it --
///     it fires before the switch even runs in the legacy source), so <see cref="PlayTimeClobberValue" /> is
///     exposed as a named constant for the caller (<c>PlaytimeBuffService</c>) to apply unconditionally,
///     independently of <see cref="Result.Applied" />.
/// </remarks>
public static class PlaytimeBuffResolver
{
    /// <summary>
    ///     The fixed value <c>wAvatar.aPlayTime2</c> is unconditionally hard-reset to on every op97 request,
    ///     before the sort switch runs (S04_MyWork02.cpp:12990-12992) -- see this type's second remarks block.
    /// </summary>
    public const int PlayTimeClobberValue = 300;

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
