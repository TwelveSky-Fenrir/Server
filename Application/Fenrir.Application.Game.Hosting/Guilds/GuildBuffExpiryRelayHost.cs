using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

public sealed class GuildBuffExpiryRelayHost(
    ZoneRegistry zones,
    IGuildBuffExpiryRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildBuffExpiryRelayHost> logger) : BackgroundService, IGuildBuffExpiryRelayQueue
{
    private const int QueueCapacity = 64;
    private const int MaxDrainedPerCycle = 32;

    private readonly Channel<GuildBuffExpiryRelayEntry> _outbox = Channel.CreateBounded<GuildBuffExpiryRelayEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        public bool Enqueue(GuildBuffExpiryRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard guild-buff-expiry relay outbox full on shard {ShardId}; dropping the fan-out for " +
            "guild {GuildId} (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.GuildId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.GuildBuffExpiryRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Guild-buff-expiry relay poll failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

        public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await FlushOutboundAsync(ct).ConfigureAwait(false);
        await DeliverInboundAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask FlushOutboundAsync(CancellationToken ct)
    {
        var reader = _outbox.Reader;
        var drained = 0;

        while (drained < MaxDrainedPerCycle && reader.TryRead(out var entry))
        {
            drained++;
            try
            {
                await relay.PublishAsync(entry, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to publish a guild-buff-expiry push for guild {GuildId} to the cross-shard relay " +
                    "from shard {ShardId}; cross-shard fan-out for this one push is lost (same-shard delivery " +
                    "already happened)", entry.GuildId, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.GuildBuffExpiryRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                var command = new GuildBuffExpiryZoneCommand(dto.GuildId, dto.NewBuffTime);
                foreach (var zone in zones.Zones)
                    zone.PostGuildBuffExpiryCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed guild-buff-expiry push {RelayId} (guild {GuildId}) on shard {ShardId}",
                    dto.RelayId, dto.GuildId, shardId);
            }
    }
}
