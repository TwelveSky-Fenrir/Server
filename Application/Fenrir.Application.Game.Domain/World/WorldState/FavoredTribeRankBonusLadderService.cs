using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     The one-shot pending-flag check-and-trigger for <see cref="FavoredTribeRankBonusLadder" />, and the sole
///     producer of the tSort=1234 cross-cluster broadcast. Reads <see cref="WorldStateService.World" />'s
///     <c>UpdateTribePoint</c> flag (the same cross-process flag <see cref="WorldStateService.SetUpdateTribePointFlag" />
///     's
///     own remarks describe -- ts25playuser writes <see cref="PendingFlagValue" /> when fresh daily tribe points
///     are pending, ts25center consumes it to <see cref="ConsumedFlagValue" />); only when it currently reads
///     <see cref="PendingFlagValue" /> does <see cref="TickIfPendingAsync" /> flip it to
///     <see cref="ConsumedFlagValue" />, apply the ladder, and -- only if both of those persist successfully --
///     broadcast the 4 fresh totals to every connected zone/player. In the shipped legacy cluster nothing ever
///     writes the flag to <see cref="PendingFlagValue" /> (see this class's own remarks), so in practice this
///     check runs every 6th tick and is (almost) always a no-op -- reproduced here exactly, not special-cased
///     away.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S07_MyGame01.cpp:199-267 (<c>MyGame::Logic</c>) -- the whole cited block
///     runs unguarded by any build macro ; :240-244 (6-tick cadence, <c>mTickCount % 6</c>) ; :253-266 -- the
///     live, compiled check-and-trigger site: reads the flag, and only calls the ladder recompute (always with
///     "do not rotate") when the flag reads as 1, flipping it to 2 FIRST as a distinct write, then persisting
///     the 4 recomputed totals as a second distinct write, then -- only if that second write succeeds --
///     building the 130-byte tSort=1234 payload and calling <c>BroadcastZone</c> ; :278-288
///     (<c>MyGame::BroadcastZone</c>) -- fan-out to every connected zone-server link ;
///     Server/ts25center/S08_MyDB.cpp:138-211 (<c>GetTribePoint(BOOL)</c>) -- the <c>#ifndef USE_NEW_CLAN_POINT</c>
///     branch (140-193, the one that compiles) is the "high tribe" biased recompute this class's own formula
///     class (<see cref="FavoredTribeRankBonusLadder" />) reproduces ; :446 (commented-out call, inside the
///     always-compiled guard at line 445) and lines 380-448 -- confirms the only call path that would ever
///     request "rotate the favored tribe" is dead (commented out), even though it sits inside an otherwise-live
///     function -- so this class never advances <see cref="WorldStateService.World" />'s <c>HighTribe</c>
///     itself, it only ever reads the value already recorded there ; Server/ts25playuser/S08_MyDB.cpp:296-319,
///     Server/ts25playuser/S07_MyGame01.cpp:449-480 (both entirely inside <c>#ifdef USE_NEW_CLAN_POINT</c>) --
///     confirms the only code anywhere in the cluster that ever writes the flag to <see cref="PendingFlagValue" />
///     is gated behind the never-defined <c>USE_NEW_CLAN_POINT</c> macro, i.e. dead in every build ; no other
///     writer of this flag exists anywhere in <c>Server/</c> ; Server/BuildEU33/DB/nxtserver.sql:1310-1358,
///     1355-1356 (schema: favored-tribe column default 0, flag column default 0) and line 1363 (seed row:
///     favored-tribe column seeded to 2, flag column seeded to 0) ; Server/BuildEU33/ServerInfo.ini:87-95
///     (Center.Server <c>TimeLogic=1000</c>) -- the ~6-second real-time cadence of the poll.
///     <para>
///         Fenrir-schema note: <c>game.WorldState.HighTribe</c> is nullable and, unlike the legacy's own seed
///         (which always starts the favored tribe at 2), Fenrir's own <c>usp_WorldState_EnsureInitialized</c>
///         does not seed a default favored tribe (schema/seed ownership is fenrir-database-engineer's). A null
///         <c>HighTribe</c> at the moment the flag is ever pending has no legacy behavior to mirror (the legacy
///         schema guarantees a value) -- this class treats it the same as any other "cannot complete this run"
///         case: skip, log, leave every totals field untouched, matching this mechanism's own "flag read fails
///         -&gt; skip silently, same as flag not pending" posture. No broadcast fires in this case either.
///     </para>
///     <para>
///         Scheduling note (shared with <see cref="TribePointLevelRecomputeService" />): both mechanisms
///         unconditionally overwrite the same 4 persisted totals, and the legacy source does not arbitrate
///         between them. <c>Fenrir.Application.Game.Hosting.World.WorldState.TribePointRecomputeHost</c> always
///         runs <see cref="TribePointLevelRecomputeService.RecomputeAsync" /> first and this class's
///         <see cref="TickIfPendingAsync" /> second within the same due tick, mirroring the legacy tick handler's
///         own sequential call order (S07_MyGame01.cpp:240-244 before :253-266) -- so on the rare/never-
///         organically-reached cycle where this flag is pending, the ladder's result is the one left standing
///         for that cycle, exactly as the legacy call order would produce. Only this path ever broadcasts;
///         <see cref="TribePointLevelRecomputeService" />'s own always-active recompute never does.
///     </para>
/// </remarks>
public sealed class FavoredTribeRankBonusLadderService(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ILogger<FavoredTribeRankBonusLadderService> logger)
{
    /// <summary>ts25playuser's "fresh daily tribe points pending" write -- see class remarks.</summary>
    public const short PendingFlagValue = 1;

    /// <summary>ts25center's own "already consumed this pending request" write -- see class remarks.</summary>
    public const short ConsumedFlagValue = 2;

    /// <summary>
    ///     No-ops unless the flag currently reads <see cref="PendingFlagValue" />. When it does, runs the full
    ///     4-step sequence in order, aborting (with no broadcast) as soon as any step fails:
    ///     <list type="number">
    ///         <item>
    ///             Immediately persists the flag flip to <see cref="ConsumedFlagValue" /> via
    ///             <see cref="WorldStateService.TryConsumeUpdateTribePointFlagAsync" />. If this write fails,
    ///             the flag is left pending and the whole sequence retries from scratch next poll.
    ///         </item>
    ///         <item>
    ///             Reads the currently-recorded favored tribe (<see cref="WorldStateService.World" />'s
    ///             <c>HighTribe</c>); if none is recorded, logs and stops -- the flag stays consumed (this
    ///             specific request is now permanently lost, per the contract), but nothing further runs.
    ///         </item>
    ///         <item>
    ///             Computes the 4 totals via <see cref="FavoredTribeRankBonusLadder.ComputeTotals" /> and
    ///             immediately persists them via
    ///             <see cref="WorldStateService.TryOverwriteTribePointTotalsAsync" />. If any tribe's write
    ///             fails, the in-memory mirror already reflects the new totals (not rolled back) but no
    ///             broadcast fires -- this request is permanently lost, never retried.
    ///         </item>
    ///         <item>
    ///             Only once that persist step fully succeeds: broadcasts the 4 totals to every connected zone
    ///             via <see cref="ZoneEventBroadcaster.AnnounceTribePointTotals" /> (tSort=1234).
    ///         </item>
    ///     </list>
    /// </summary>
    public async Task TickIfPendingAsync(CancellationToken ct)
    {
        if (!await worldState.TryConsumeUpdateTribePointFlagAsync(PendingFlagValue, ConsumedFlagValue, ct)
                .ConfigureAwait(false))
            return;

        if (worldState.World.HighTribe is not { } favoredTribeId)
        {
            logger.LogWarning(
                "FavoredTribeRankBonusLadder: pending flag consumed but no favored tribe is recorded (HighTribe is null) -- skipping this run, previous totals left unchanged, no broadcast");
            return;
        }

        var totals = FavoredTribeRankBonusLadder.ComputeTotals(favoredTribeId);

        if (!await worldState.TryOverwriteTribePointTotalsAsync(totals, ct).ConfigureAwait(false))
        {
            logger.LogError(
                "FavoredTribeRankBonusLadder: totals persist failed after the flag was already consumed -- this request is now permanently lost, no broadcast");
            return;
        }

        broadcaster.AnnounceTribePointTotals(totals);

        logger.LogInformation(
            "FavoredTribeRankBonusLadder: applied and broadcast -- favored tribe {FavoredTribeId}, totals Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            favoredTribeId, totals[0], totals[1], totals[2], totals[3]);
    }
}
