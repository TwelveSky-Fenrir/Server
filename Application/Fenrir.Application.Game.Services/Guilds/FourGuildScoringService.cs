using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

/// <summary>One entry of the four-guild live-scoring leaderboard (guild name + its four-guild point total).</summary>
public readonly record struct FourGuildStanding(int GuildId, string Name, int Points);

/// <summary>
///     Four-guild live scoring (behavior contract C17, Part B). Owns both halves of the legacy's split
///     ts25zone-accrual / ts25extra-scoring flow, collapsed into one Game-side service:
///     <list type="bullet">
///         <item>
///             <see cref="AccrueKillPointAsync" /> -- credit the killer's guild +1 point per enemy-tribe kill
///             scored while the zone is in the zone-049 event mode (B1;
///             Server/ts25zone/S07_MyGame03.cpp:3098-3100 -&gt; Server/ts25extra/S08_MyDB.cpp:1146-1148).
///             Best-effort and silent: a failed increment is never reported to the killing client, matching
///             the legacy's fire-and-forget accrual.
///         </item>
///         <item>
///             <see cref="RecomputeAsync" /> -- recompute the top-three positive-point standings (B2;
///             Server/ts25extra/S08_MyDB.cpp:1109-1140, LoadFourGuild) and publish them into
///             <see cref="CurrentStandings" /> for display/broadcast. The legacy recomputes at boot and every
///             ~10 seconds of its own tick loop (Server/ts25extra/S07_MyGame01.cpp:42,98-101,153-155); the
///             driving cadence is a hosted service's concern, this service only does one recompute per call.
///         </item>
///     </list>
///     Backed by <see cref="IFourGuildScoringRepository" /> over game.Guilds.Points (legacy gPoint) -- the same
///     counter the general guild ranking board reads, exactly as the legacy four-guild event and the guild
///     ranking share gPoint.
/// </summary>
public sealed class FourGuildScoringService(
    IFourGuildScoringRepository scoring,
    ILogger<FourGuildScoringService> logger)
{
    /// <summary>Server/ts25zone/S07_MyGame03.cpp:3100 -- always exactly +1 per qualifying kill.</summary>
    public const int KillPointDelta = 1;

    /// <summary>Server/ts25extra/S08_MyDB.cpp:1126-1140 -- the leaderboard is the top three standings.</summary>
    public const int LeaderboardSize = 3;

    // Reference assignment is atomic; volatile publishes the new immutable snapshot to broadcast readers on
    // other threads. Readers always observe a fully-built, self-consistent standings array (old or new).
    private volatile IReadOnlyList<FourGuildStanding> _standings = [];

    /// <summary>The most recently recomputed standings; empty until the first successful <see cref="RecomputeAsync" />.</summary>
    public IReadOnlyList<FourGuildStanding> CurrentStandings => _standings;

    /// <summary>
    ///     Credit <paramref name="guildId" /> +1 four-guild point. Silent by contract -- an unknown/disbanded
    ///     guild is a no-op in the procedure, and any infrastructure fault is swallowed (logged) rather than
    ///     propagated into the kill-reward pipeline that calls this.
    /// </summary>
    public async Task AccrueKillPointAsync(int guildId, CancellationToken ct)
    {
        try
        {
            await scoring.AddPointsAsync(guildId, KillPointDelta, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Four-guild point accrual for guild {GuildId} failed -- point lost, kill reward unaffected",
                guildId);
        }
    }

    /// <summary>
    ///     Recompute and publish the top-three positive-point standings. A failed read leaves the previously
    ///     published standings in place (contract: a failed leaderboard read is silent), never clears them.
    /// </summary>
    public async Task RecomputeAsync(CancellationToken ct)
    {
        IReadOnlyList<GuildRankingRowDto> rows;
        try
        {
            rows = await scoring.GetLeaderboardAsync(LeaderboardSize, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Four-guild leaderboard recompute failed -- previous standings left in place");
            return;
        }

        var standings = new FourGuildStanding[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            standings[i] = new FourGuildStanding(rows[i].GuildId, rows[i].Name, rows[i].Points);

        _standings = standings;

        logger.LogDebug("Four-guild leaderboard recomputed: {Count} guild(s) with positive points", standings.Length);
    }
}
