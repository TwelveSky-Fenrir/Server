using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

public sealed class GuildStateRelayHost(
    ZoneRegistry zones,
    IGuildStateRelayRepository relay,
    IGuildRepository guilds,
    IOptions<GameServerOptions> options,
    ILogger<GuildStateRelayHost> logger)
    : ClusterRelayPumpBase<GuildStateRelayEntry, GuildStateRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.GuildStateRelayPollIntervalSeconds),
            options.Value.GuildStateRelayRetentionSeconds),
        IGuildStateRelayQueue
{
    private const int QueueCapacity = 256;

    private const int AgmRoleSort = 9;
    private const int MemberTitleSort = 10;

    protected override async ValueTask DeliverAsync(GuildStateRelayDto dto, CancellationToken ct)
    {
        switch ((GuildStateRelayKind)dto.Kind)
        {
            case GuildStateRelayKind.MembershipRemoved:
                ApplyMembership(dto);
                break;

            case GuildStateRelayKind.MembershipRoleChanged:
                ApplyMembership(dto);
                await BroadcastGuildInfoAsync(dto.GuildId, AgmRoleSort, ct).ConfigureAwait(false);
                break;

            case GuildStateRelayKind.MembershipTitleChanged:
                ApplyMembership(dto);
                await BroadcastGuildInfoAsync(dto.GuildId, MemberTitleSort, ct).ConfigureAwait(false);
                break;

            case GuildStateRelayKind.BuffActivation:
                var activation = new GuildBuffActivationZoneCommand(dto.GuildId, dto.BuffType, dto.BuffActive);
                foreach (var zone in zones.Zones)
                    zone.PostGuildBuffActivationCommand(in activation);
                break;

            default:
                logger.LogWarning("Relayed guild-state row {RelayId} has unrecognized Kind {Kind}; dropped",
                    dto.RelayId, dto.Kind);
                break;
        }
    }

    private void ApplyMembership(GuildStateRelayDto dto)
    {
        if (dto.TargetCharacterId is not { } targetCharacterId)
            return;

        if (!zones.TryGetPlayerAndZone(targetCharacterId, out _, out var targetZone))
            return;

        if (!targetZone.PostGuildCommand(new GuildMembershipZoneCommand(targetCharacterId, dto.NewGuildId,
                dto.GuildName, dto.GuildRoleDb, dto.GuildCallName)))
            logger.LogError(
                "Zone {MapId} guild inbox full: dropped relayed guild-membership change for character {CharacterId}",
                targetZone.MapId, targetCharacterId);
    }

    private async ValueTask BroadcastGuildInfoAsync(int guildId, int sort, CancellationToken ct)
    {
        if (await guilds.GetByIdAsync(guildId, ct).ConfigureAwait(false) is not { } guild)
            return;

        var roster = await guilds.GetRosterAsync(guildId, ct).ConfigureAwait(false);
        var notices = await guilds.GetNoticesAsync(guildId, ct).ConfigureAwait(false);

        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, sort, GuildInfoProjection.Build(guild, roster,
            notices));
    }

    protected override void OnOutboxFull(GuildStateRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard guild-state relay outbox full on shard {ShardId}; dropping one {Kind} row for " +
            "guild {GuildId} (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.Kind, entry.GuildId);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Guild-state relay outbound flush failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Guild-state relay inbound delivery failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnPublishFailed(GuildStateRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a {Kind} guild-state row for guild {GuildId} to the cross-shard relay from " +
            "shard {ShardId}; cross-shard fan-out for this one change is lost (SQL is durable, remote shards " +
            "converge on next world entry)", entry.Kind, entry.GuildId, options.Value.ShardId);
    }

    protected override void OnDeliveryFailed(GuildStateRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to locally deliver relayed guild-state row {RelayId} (kind {Kind}, guild {GuildId}) on shard {ShardId}",
            dto.RelayId, dto.Kind, dto.GuildId, options.Value.ShardId);
    }
}
