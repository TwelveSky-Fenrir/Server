using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard fan-out, Game-side, for the proxy-shop rental-extension consumables' best-effort
///     live-registry mirror update (world.Items 567/592/8422/8423 via CZ_USE_INVENTORY_ITEM_SEND). This
///     deliberately hardens past a legacy structural limitation rather than reproducing it: legacy's own
///     in-place update (Server/ts25zone/S04_MyWork03.cpp:2632-2675) only ever applied on the single process
///     holding a non-null pointer to the live shop registry (server/zone 37 under the always-active
///     PPSHOP_V2 build variant, Server/Header/Protocol/DEFINE.h:95), silently no-opping everywhere else with
///     no cross-process resynchronization path at all -- an explicit open decision the originating behavior
///     contract left to whichever engineer implemented this, not a legacy behavior being reproduced. See
///     <c>UseInventoryItemService.ResolveProxyShopRentalExtensionAsync</c>'s own remarks for the full trail.
/// </summary>
/// <remarks>
///     <para>
///         Same "one instance, three registrations" composition shape as <see cref="GuildTribeBroadcastRelayHost" />,
///         drastically simplified: a single-purpose row (no <c>Kind</c> multiplexing -- this table carries
///         exactly one message shape) and fan-out delivery that only ever finds a match on whichever ONE
///         shard currently hosts <see cref="ProxyShopZonePolicy.ZoneNumber" />'s own <see cref="Zone" />
///         instance (Fenrir shards <see cref="Zone" /> instances disjointly by map, see
///         <see cref="ProxyShopZonePolicy" />'s own remarks) -- every other shard's poll simply finds no
///         zone-37 <see cref="Zone" /> instance to update and moves on, mirroring
///         <see cref="Zone.TryUpdateProxyShopExpiration" />'s own existing "silent miss elsewhere" contract.
///     </para>
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and
///         publishes each entry via <see cref="IProxyShopExpirationRelayRepository.PublishAsync" /> so every
///         OTHER live shard's own next poll picks it up; (2) calls
///         <see cref="IProxyShopExpirationRelayRepository.PollAsync" /> for rows some OTHER shard published
///         since this shard's own last poll and applies each to this shard's own locally-hosted zone-37
///         instance, if any. <c>UseInventoryItemService</c> always enqueues here immediately after its own
///         existing, unchanged, synchronous same-shard <see cref="Zone.TryUpdateProxyShopExpiration" />
///         attempt -- regardless of whether that local attempt hit or missed (a miss on the publishing shard
///         is the overwhelmingly common case, since the acting character is rarely standing in zone 37 at
///         the moment they use the item) -- exactly the same "local attempt first, relay is a separate
///         cross-shard leg" shape <see cref="GuildTribeBroadcastRelayHost" /> already established. A shard
///         never re-applies its own published rows to itself: the poll's own SQL predicate excludes
///         <c>SourceShardId = @ShardId</c>.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both the publish and delivery loops -- one failed publish or one
///         malformed inbound row must never block the rest of the batch or take the poll cycle down, the
///         same posture <see cref="GuildTribeBroadcastRelayHost" /> already applies.
///     </para>
/// </remarks>
public sealed class ProxyShopExpirationRelayHost(
    ZoneRegistry zones,
    IProxyShopExpirationRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ProxyShopExpirationRelayHost> logger) : BackgroundService, IProxyShopExpirationRelayQueue
{
    // Rental-extension consumable use is human-click-speed, not a per-tick/per-combat volume -- same sizing
    // rationale as GuildTribeBroadcastRelayHost's own outbox, generously sized regardless.
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private readonly Channel<ProxyShopExpirationRelayEntry> _outbox =
        Channel.CreateBounded<ProxyShopExpirationRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                // Wait mode's TryWrite returns false immediately when full (only WriteAsync actually awaits
                // space) -- the honest, non-blocking backpressure signal Enqueue needs, since it only ever
                // calls TryWrite. Same reasoning as GuildTribeBroadcastRelayHost's own choice of Wait over
                // DropWrite.
                FullMode = BoundedChannelFullMode.Wait
            });

    /// <inheritdoc />
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
                // A missed cycle just delays the live-registry mirror by one more poll interval -- never
                // worth crashing the GameServer over (the durable date is already saved regardless, and the
                // periodic expiry sweep force-closing a stale entry is the only thing this delays fixing).
                logger.LogError(ex, "Proxy-shop expiration relay poll failed for shard {ShardId}",
                    options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
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
                // Dropped, not requeued -- same "no retry, accepted residual gap" posture as
                // GuildTribeBroadcastRelayHost's own failed-publish handling.
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

    /// <summary>
    ///     Applies the update to this shard's own zone-37 <see cref="Zone" /> instance, if this shard hosts
    ///     one -- a silent no-op on every other shard, matching
    ///     <see cref="Zone.TryUpdateProxyShopExpiration" />'s own "miss is expected and unremarkable"
    ///     contract.
    /// </summary>
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
