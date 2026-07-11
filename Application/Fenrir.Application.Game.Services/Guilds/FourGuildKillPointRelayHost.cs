using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

/// <summary>
///     C17 Part B1 composition root: owns the one bounded relay channel bridging a zone's own tick thread
///     (<c>Zone.KillFeed.cs</c>'s <c>RecordEnemyKillForFeed</c>, Domain layer) to
///     <see cref="FourGuildScoringService.AccrueKillPointAsync" /> (this same Services layer), and drives its
///     drain loop as a <see cref="BackgroundService" /> -- same "one instance, three registrations"
///     composition-root shape as <c>Hosting.EventLog.EventLogFlushHost</c>. Lives in
///     <c>Fenrir.Application.Game.Services</c> rather than <c>Fenrir.Application.Game.Hosting</c> specifically
///     because <c>Fenrir.Application.Game.Hosting</c> has no project reference to this project (Hosting wires
///     Domain/Data, never Services, directly) -- see <see cref="IFourGuildKillPointQueue" />'s own remarks for
///     the full reasoning. This project already carries the <c>Microsoft.Extensions.Hosting.Abstractions</c>
///     package (for the various <c>*ServicesServiceCollectionExtensions.cs</c> files' own
///     <c>IServiceCollection</c> usage), which is also where <see cref="BackgroundService" /> itself lives, so
///     no new project reference is needed to host this here.
/// </summary>
/// <remarks>
///     Réf. contract: <see cref="FourGuildScoringService" />'s own remarks, Part B1 -- this host only owns the
///     bridge/drain plumbing (bounded channel, backpressure logging, per-item drain-and-forward); the actual
///     accrual semantics (silent on an unknown/disbanded guild, every infrastructure fault swallowed and
///     logged rather than propagated) live entirely in
///     <see cref="FourGuildScoringService.AccrueKillPointAsync" /> itself and are not duplicated here.
/// </remarks>
public sealed class FourGuildKillPointRelayHost : BackgroundService, IFourGuildKillPointQueue
{
    /// <summary>
    ///     Generous relative to expected per-zone kill cadence (a handful of Zone049-type kills per second at
    ///     most across an entire shard) -- sized the same order of magnitude as
    ///     <c>EventLogQueue.DefaultCapacity</c> / 4, since this queue carries a single <see langword="int" />
    ///     per entry rather than a full audit row.
    /// </summary>
    public const int Capacity = 1024;

    private readonly Channel<int> _channel;
    private readonly ILogger<FourGuildKillPointRelayHost> _logger;
    private readonly FourGuildScoringService _scoring;

    public FourGuildKillPointRelayHost(FourGuildScoringService scoring, ILogger<FourGuildKillPointRelayHost> logger)
    {
        _scoring = scoring;
        _logger = logger;

        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            // NOT DropWrite -- same reasoning as EventLogQueue's own channel: Wait mode's TryWrite returns
            // false immediately and synchronously when full, an honest, observable backpressure signal;
            // DropWrite's TryWrite would return true even while silently discarding the entry, so Enqueue
            // below could never report or log a drop.
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <inheritdoc />
    public bool Enqueue(int guildId)
    {
        if (_channel.Writer.TryWrite(guildId))
            return true;

        _logger.LogWarning(
            "Four-guild kill-point relay queue full; point for guild {GuildId} dropped (sustained DB unavailability?)",
            guildId);
        return false;
    }

    /// <summary>
    ///     Drains the channel and forwards each entry to <see cref="FourGuildScoringService.AccrueKillPointAsync" />
    ///     one at a time -- no batching is needed here (unlike <c>EventLogQueue</c>'s TVP batch flush) since
    ///     <c>AccrueKillPointAsync</c> is already a single, cheap, self-contained stored-procedure call that
    ///     never throws anything but <see cref="OperationCanceledException" />.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var guildId in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                await _scoring.AccrueKillPointAsync(guildId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
