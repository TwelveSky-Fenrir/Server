using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Cluster.Relay;
using Fenrir.Core.Packets.Shared;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GuildTribeBroadcastRelayHost(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildTribeBroadcastRelayHost> logger)
    : ClusterRelayPumpBase<GuildTribeBroadcastRelayEntry, GuildTribeBroadcastRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.GuildTribeBroadcastPollIntervalSeconds),
            options.Value.GuildTribeBroadcastRetentionSeconds),
        IGuildTribeBroadcastRelayQueue
{
    private const int QueueCapacity = 1024;

    protected override ValueTask DeliverAsync(GuildTribeBroadcastRelayDto dto, CancellationToken ct)
    {
        DeliverLocally(dto);
        return ValueTask.CompletedTask;
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

    protected override void OnOutboxFull(GuildTribeBroadcastRelayEntry entry) =>
        logger.LogWarning(
            "Cross-shard guild/tribe broadcast relay outbox full on shard {ShardId}; dropping one {Kind} " +
            "broadcast (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.Kind);

    protected override void OnOutboundFlushFailed(Exception ex) =>
        logger.LogError(ex, "Guild/tribe broadcast outbound flush failed for shard {ShardId}",
            options.Value.ShardId);

    protected override void OnInboundDeliveryFailed(Exception ex) =>
        logger.LogError(ex, "Guild/tribe broadcast inbound delivery failed for shard {ShardId}",
            options.Value.ShardId);

    protected override void OnPublishFailed(GuildTribeBroadcastRelayEntry entry, Exception ex) =>
        logger.LogError(ex,
            "Failed to publish a {Kind} broadcast to the cross-shard relay from shard {ShardId}; " +
            "cross-shard fan-out for this one message is lost (same-shard delivery already happened)",
            entry.Kind, options.Value.ShardId);

    protected override void OnDeliveryFailed(GuildTribeBroadcastRelayDto dto, Exception ex) =>
        logger.LogError(ex,
            "Failed to locally deliver relayed broadcast {RelayId} (kind {Kind}) on shard {ShardId}",
            dto.RelayId, dto.Kind, options.Value.ShardId);
}
