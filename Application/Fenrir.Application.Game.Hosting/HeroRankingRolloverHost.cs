using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Polls whether the weekly hero-ranking Current-&gt;Previous rollover (<c>game.usp_HeroRanking_Rollover</c>)
///     is due. All the actual gating (the 7-day sentinel, the flip itself) lives in the proc, which is safe
///     to call redundantly and concurrently from every shard -- this host does not need to coordinate with
///     any other GameServer process, unlike <see cref="ShardPartitionGuard" />.
/// </summary>
/// <remarks>
///     On a successful flip, also fans out a reset-and-notify trigger to every hosted zone so already-connected
///     clients don't keep a stale running total until their next login -- the Fenrir counterpart of legacy's
///     <c>FIX_HERO_RANK_RESET_PROBLEM</c> per-tick Monday-00:00 wall-clock check
///     (Server/ts25zone/S07_MyGame01.cpp:2507-2515), which resets any connected user's live counter to zero and
///     pushes <c>S904UPDATE_HERO_POINT</c> (Server/Header/Protocol/STRUCT.h:1650) to that one connection only
///     (Server/ts25zone/S05_MyTransfer.cpp:519-542's <c>MyUser*</c>-targeted <c>B_AVATAR_CHANGE_INFO_2</c>
///     overload -- unicast, never a zone-wide broadcast). The actual per-player reset-and-notify pair runs on
///     each zone's own tick thread -- see <c>Zone.ApplyHeroRankingRolloverReset</c> -- this host only detects
///     the flip and posts the trigger; PlayerRuntimeState must never be mutated from this thread directly.
/// </remarks>
public sealed class HeroRankingRolloverHost(
    IHeroRankingRepository heroRankings,
    ZoneRegistry zones,
    IOptions<GameServerOptions> options,
    ILogger<HeroRankingRolloverHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(opts.HeroRankingRolloverCheckIntervalMinutes));

        do
        {
            try
            {
                if (await heroRankings.RolloverIfDueAsync(stoppingToken).ConfigureAwait(false))
                {
                    logger.LogInformation(
                        "Hero ranking rollover: Current period flipped into Previous (7-day sentinel elapsed)");

                    NotifyConnectedSessions();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed check just delays the flip to the next tick -- never worth crashing the GameServer.
                logger.LogError(ex, "Hero ranking rollover check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>
    ///     Posts one <see cref="HeroRankingRolloverZoneCommand" /> per hosted zone so each zone's own tick thread
    ///     resets and notifies its own currently connected players (<c>Zone.ApplyHeroRankingRolloverReset</c>).
    ///     Best-effort: a dropped post (this shard has no hosted zones yet, or an already-saturated 4-slot inbox,
    ///     exceedingly unlikely for an event this rare) just leaves that zone's stale mirrors to self-heal on next
    ///     login -- the same posture already accepted for every other write-behind-backed counter here.
    /// </summary>
    private void NotifyConnectedSessions()
    {
        foreach (var zone in zones.Zones)
            zone.PostHeroRankingRolloverReset();
    }
}
