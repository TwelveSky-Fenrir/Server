using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Orchestrates the level/rebirth-based tribe-point recompute (contract side effects 1-3): fetches the
///     persisted character/avatar roster through <see cref="ITribePointRosterGateway" />, applies the pure
///     <see cref="TribePointLevelRecompute.ComputeTotals" /> formula, and -- only on a successful read -- fully
///     overwrites all 4 <see cref="WorldStateService" /> tribe-point totals in one go via
///     <see cref="WorldStateService.SetTribePoints" />, matching the contract's "full recompute-and-overwrite,
///     never an incremental delta." Driven by <c>Fenrir.Application.Game.Hosting.World.WorldState.TribePointRecomputeHost</c>,
///     once at boot and every 6th ~1-second tick thereafter.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S08_MyDB.cpp:17-54 (<c>GetWorldInfo</c>, boot-time call site at line 42) ;
///     Server/ts25center/S07_MyGame01.cpp:240-244 (periodic call site, gated by <c>(mTickCount % 6) == 0</c>) ;
///     Server/ts25center/S07_MyGame01.cpp:30-33 (<c>MyGame::Init</c>) -- confirms the tribe-point totals are
///     zero-initialized at boot, the value that persists if every recompute attempt fails, exactly what
///     <see cref="LoggingOnlyTribePointRosterGateway" /> reproduces until a real roster gateway is wired.
///     <para>
///         Known legacy implementation bug -- deliberately NOT reproduced here: the live query
///         (<c>GetTribePoint</c>, S08_MyDB.cpp:107-136) reads from <c>mSERVER_INFO.mDBTable[9]</c> (RankInfo --
///         only carries <c>uCharIdx</c>/<c>aExp1</c>/<c>aExp1Update</c>/<c>aExp2</c>/<c>aExp2Update</c>, no
///         level/tribe/rebirth columns at all), not <c>mDBTable[2]</c> (AvatarInfo, the table that actually
///         carries <c>aTribe</c>/<c>aLevel1</c>/<c>aLevel2</c>/<c>aRebirthNum</c> -- see
///         Server/BuildEU33/ServerInfo.ini:16-25 and Server/BuildEU33/DB/nxtserver.sql:24-41,118). This almost
///         certainly means the live query fails outright in the shipped server, so this recompute likely never
///         successfully produced a value there. The fix for this port: always source from the genuine
///         character/avatar roster via <see cref="ITribePointRosterGateway" /> -- the formula itself
///         (<see cref="TribePointLevelRecompute" />) is correct as shipped and is preserved unchanged.
///     </para>
/// </remarks>
public sealed class TribePointLevelRecomputeService(
    ITribePointRosterGateway rosterGateway,
    WorldStateService worldState,
    ILogger<TribePointLevelRecomputeService> logger)
{
    /// <summary>
    ///     Fetches the roster and, only on success, overwrites all 4 tribe-point totals. A failed/unavailable
    ///     read (contract: "the per-tribe query cannot execute or returns nothing") aborts silently -- the
    ///     previously-held totals are left exactly as they were, with no partial per-tribe write, and nothing
    ///     here is ever visible to a connected player beyond server-side logging.
    /// </summary>
    public async Task RecomputeAsync(CancellationToken ct)
    {
        IReadOnlyList<TribeRosterCharacterSnapshot>? roster;
        try
        {
            roster = await rosterGateway.GetRosterAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TribePointLevelRecompute: roster read threw -- previous totals left unchanged for this run");
            return;
        }

        if (roster is null)
        {
            logger.LogDebug(
                "TribePointLevelRecompute: roster unavailable -- previous totals left unchanged for this run");
            return;
        }

        var totals = TribePointLevelRecompute.ComputeTotals(roster);
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            worldState.SetTribePoints(tribeId, totals[tribeId]);

        logger.LogInformation(
            "TribePointLevelRecompute: totals overwritten -- Tribe0={Tribe0} Tribe1={Tribe1} Tribe2={Tribe2} Tribe3={Tribe3}",
            totals[0], totals[1], totals[2], totals[3]);
    }
}
