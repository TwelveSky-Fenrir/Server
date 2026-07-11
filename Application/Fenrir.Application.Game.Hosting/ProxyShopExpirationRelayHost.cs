using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class ProxyShopExpirationRelayHost(
    ZoneRegistry zones,
    IProxyShopExpirationRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ProxyShopExpirationRelayHost> logger) : BackgroundService, IProxyShopExpirationRelayQueue
{
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private readonly Channel<ProxyShopExpirationRelayEntry> _outbox =
        Channel.CreateBounded<ProxyShopExpirationRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        public bool Enqueue(ProxyShopExpirationRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard proxy-shop expiration relay outbox full on shard {ShardId}; dropping the cross-shard " +
            "leg of one rental extension for character {CharacterId} (the durable game.OfflineShops.ShopDate " +
            "write already succeeded either way)",
            options.Value.ShardId, entry.CharacterId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ProxyShopExpirationRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Proxy-shop expiration relay poll failed for shard {ShardId}",
                    options.Value.ShardId);
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
                    "Failed to publish a proxy-shop expiration relay row for character {CharacterId} from " +
                    "shard {ShardId}; the cross-shard leg of this one extension is lost (the durable date is " +
                    "already saved)",
                    entry.CharacterId, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.ProxyShopExpirationRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                DeliverLocally(dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed proxy-shop expiration update {RelayId} for character " +
                    "{CharacterId} on shard {ShardId}",
                    dto.RelayId, dto.CharacterId, shardId);
            }
    }

        private void DeliverLocally(ProxyShopExpirationRelayDto dto)
    {
        foreach (var zone in zones.Zones)
        {
            if (zone.MapId != ProxyShopZonePolicy.ZoneNumber)
                continue;

            zone.TryUpdateProxyShopExpiration(dto.CharacterId, dto.NewExpirationDate);
            return;
        }
    }
}
