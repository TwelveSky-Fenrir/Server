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
///     parameter rather than querying anything -- the handler/service passes 1 (a tribe's own default slot,
///     matching the legacy's own <c>mTribeSymbol[i]=i</c> world-init default with no alliance in play), making
///     only sort=1 reachable today. This is real, correctly-gated, but currently partially unreachable, same
///     posture as <see cref="World.PlayerRuntimeState.AnimalTime" />.
///     <para>
///         Both wire preconditions cited alongside the tier gate at the same source location
///         (Server/ts25zone/S04_MyWork02.cpp:13994-14112, per the progression-and-misc-systems behavior contract,
///         2026-07 round) ARE modeled here as of that contract, closing the two open follow-ups this type and
///         <c>RankBuffHandler</c> previously documented: the acting avatar's own mid-zone-transfer gate
///         (<paramref name="isMovingZone" />, unconditional, checked before anything else -- legacy's
///         <c>IsMovingZone()</c> on the REQUESTER, unlike <c>UnstunResolver</c>'s target-only check) and the
///         world-wide tribe-symbol (RvR) battle lock (<paramref name="worldBattleActive" />, sourced from
///         <c>WorldStateService.World.TribeSymbolBattle</c> -- a real, live-tracked flag despite this type's own
///         earlier remarks calling it "always 0 in Fenrir"; that claim predates
///         <c>Fenrir.Application.Game.Domain.World.ZoneWar.TribeSymbolBattleScheduler</c>/
///         <c>ZoneEventBroadcaster.AnnounceTribeSymbolBattleStarted</c>/<c>Ended</c> landing and is now stale).
///         The world-battle case is a complete, silent no-op (<see cref="Result.SilentlyIgnored" />) -- distinct
///         from every other rejection here, all of which disconnect.
///     </para>
/// </remarks>
public static class RankBuffResolver
{
    public enum Outcome
    {
        /// <summary>
        ///     Disconnect: either the acting avatar is mid a cross-shard zone transfer, the requested tier is
        ///     outside 1-7, or the tribe's (plus ally's) controlled territory-symbol count is below that tier's
        ///     threshold. The legacy source does not distinguish these on the wire -- all three disconnect the
        ///     same way, with no response.
        /// </summary>
        Rejected,

        /// <summary>
        ///     World is currently in an active tribe-symbol (RvR) battle -- the request is silently ignored in
        ///     its entirety: no reply, no disconnect, no state change of any kind.
        /// </summary>
        WorldBattleActive,

        Success
    }

    private static readonly int[] RequiredStoneCount = [1, 2, 2, 3, 3, 4, 4];

    public static Result Resolve(int sort, int stoneCount, bool isMovingZone, bool worldBattleActive)
    {
        // Acting avatar's own state, checked first and unconditionally -- matches legacy's own check order at
        // this source location (see this type's remarks).
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
