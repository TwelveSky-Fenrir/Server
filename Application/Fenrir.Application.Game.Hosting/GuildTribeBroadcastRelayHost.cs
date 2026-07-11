using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GuildTribeBroadcastRelayHost(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildTribeBroadcastRelayHost> logger) : BackgroundService, IGuildTribeBroadcastRelayQueue
{
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private readonly Channel<GuildTribeBroadcastRelayEntry> _outbox =
        Channel.CreateBounded<GuildTribeBroadcastRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        public bool Enqueue(GuildTribeBroadcastRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard guild/tribe broadcast relay outbox full on shard {ShardId}; dropping one {Kind} " +
            "broadcast (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.Kind);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.GuildTribeBroadcastPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Guild/tribe broadcast relay poll failed for shard {ShardId}",
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
                    "Failed to publish a {Kind} broadcast to the cross-shard relay from shard {ShardId}; " +
                    "cross-shard fan-out for this one message is lost (same-shard delivery already happened)",
                    entry.Kind, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.GuildTribeBroadcastRetentionSeconds;

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
                    "Failed to locally deliver relayed broadcast {RelayId} (kind {Kind}) on shard {ShardId}",
                    dto.RelayId, dto.Kind, shardId);
            }
    }

    private void DeliverLocally(GuildTribeBroadcastRelayDto dto)
    {
        switch ((GuildTribeBroadcastKind)dto.Kind)
        {
            case GuildTribeBroadcastKind.GuildAnnouncement:
                DeliverToGuild(dto.GuildId,
                    new GuildAnnouncementResponse { AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.GuildChat:
                var link = new ItemLinkInfo
                {
                    Index = dto.ItemLinkIndex ?? 0,
                    Activity = dto.ItemLinkActivity ?? 0,
                    Value = dto.ItemLinkValue ?? 0,
                    Socket = [dto.ItemLinkSocket0 ?? 0, dto.ItemLinkSocket1 ?? 0, dto.ItemLinkSocket2 ?? 0]
                };
                DeliverToGuild(dto.GuildId,
                    new GuildChatResponse { AvatarName = dto.AvatarName, Content = dto.Content, Link = link });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncement:
                DeliverToTribe(dto.Tribe, new TribeAnnouncementResponse
                    { TribeRole = dto.RoleField, AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncementScroll:
                DeliverToTribe(dto.Tribe, new TribeAnnouncementScrollResponse
                    { TribeRole = dto.RoleField, AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            default:
                logger.LogWarning("Relayed broadcast {RelayId} has unrecognized Kind {Kind}; dropped",
                    dto.RelayId, dto.Kind);
                break;
        }
    }

    private void DeliverToGuild(int? guildId, GuildAnnouncementResponse response)
    {
        if (guildId is not { } id)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.GuildId == id)
                recipient.Session.Send(response);
    }

    private void DeliverToGuild(int? guildId, GuildChatResponse response)
    {
        if (guildId is not { } id)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.GuildId == id)
                recipient.Session.Send(response);
    }

    private void DeliverToTribe(byte? tribe, TribeAnnouncementResponse response)
    {
        if (tribe is not { } t)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.Tribe == t)
                recipient.Session.Send(response);
    }

    private void DeliverToTribe(byte? tribe, TribeAnnouncementScrollResponse response)
    {
        if (tribe is not { } t)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.Tribe == t)
                recipient.Session.Send(response);
    }
}
