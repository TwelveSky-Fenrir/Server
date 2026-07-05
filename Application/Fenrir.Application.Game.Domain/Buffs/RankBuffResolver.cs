namespace Fenrir.Application.Game.Domain.Buffs;

/// <summary>
///     Pure resolver for CZ_RANK_BUFF_SEND (op111, S04_MyWork02.cpp:13994). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     <c>USE_RANK_POINT</c> is off in this build (DEFINE.h:77), so the live gate is
///     <c>ReturnSymbolNumNoMon(mWorldInfo, tribe)</c> -- a count of "symbol stones" the tribe (or its ally) holds
///     across 4 world slots, thresholds 1/2/2/3/3/4/4 for sort 1-7. Fenrir has no symbol-battle/alliance-capture
///     subsystem (<c>game.WorldStateTribes</c> only tracks a per-tribe bool, not the 4-slot count, and no code
///     ever moves a symbol between tribes), so <see cref="Resolve" /> takes the caller-supplied stone count as a
///     parameter rather than querying anything -- the handler passes 1 (a tribe's own default slot, matching the
///     legacy's own <c>mTribeSymbol[i]=i</c> world-init default with no alliance in play), making only sort=1
///     reachable today. This is real, correctly-gated, but currently partially unreachable, same posture as
///     <see cref="World.PlayerRuntimeState.AnimalTime" />. <c>mTribeSymbolBattle</c> (an unrelated world-event
///     lock) also always defaults to 0 in Fenrir, so it never blocks this opcode either.
/// </remarks>
public static class RankBuffResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly int[] RequiredStoneCount = [1, 2, 2, 3, 3, 4, 4];

    public static Result Resolve(int sort, int stoneCount)
    {
        if (sort is < 1 or > 7)
            return new Result(Outcome.Rejected);

        return stoneCount < RequiredStoneCount[sort - 1]
            ? new Result(Outcome.Rejected)
            : new Result(Outcome.Success);
    }

    public readonly record struct Result(Outcome Outcome)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
