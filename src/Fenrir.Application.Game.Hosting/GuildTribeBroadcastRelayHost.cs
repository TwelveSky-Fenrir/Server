using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.Relay;
using Fenrir.Core.Packets.Shared;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GuildTribeBroadcastRelayHost(
    ZoneRegistry zones,
    PartyRegistry parties,
    IGuildTribeBroadcastRelayRepository relay,
    IGuildRepository guilds,
    ITribeRepository tribes,
    IRelaySourceIdentityRepository relaySources,
    IEventLogQueue eventLogQueue,
    IOptions<GameServerOptions> options,
    ILogger<GuildTribeBroadcastRelayHost> logger)
    : AcknowledgedClusterRelayPumpBase<GuildTribeBroadcastRelayEntry, GuildTribeBroadcastRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.GuildTribeBroadcastPollIntervalSeconds),
            options.Value.GuildTribeBroadcastRetentionSeconds),
        IGuildTribeBroadcastRelayQueue
{
    private const int QueueCapacity = 1024;
    private const short RelayAuthorizationRejectedEventCode = 1;
    private const byte AuthorizationRejectedOutcome = 0;
    private const byte WorldChatWireRole = 1;
    private const byte TribeAnnouncementScrollWireRole = 1;
    private const int CompletedCorrelationCapacity = 4_096;

    private readonly object _completedCorrelationGate = new();
    private readonly Queue<Guid> _completedCorrelationOrder = [];
    private readonly HashSet<Guid> _completedCorrelations = [];

    protected override long GetRelayId(GuildTribeBroadcastRelayDto dto)
    {
        return dto.RelayId;
    }

    protected override async ValueTask DeliverAsync(GuildTribeBroadcastRelayDto dto, CancellationToken ct)
    {
        var authorization = await ReauthorizeAsync(dto, ct).ConfigureAwait(false);
        if (!authorization.IsAuthorized)
        {
            if (IsCompletedCorrelation(dto.CorrelationId))
                return;

            AuditAuthorizationRejection(dto, authorization.Reason);
            RememberCompletedCorrelation(dto.CorrelationId);
            return;
        }

        if (IsCompletedCorrelation(dto.CorrelationId))
            return;

        DeliverLocally(dto, authorization);
        RememberCompletedCorrelation(dto.CorrelationId);
    }

    private async ValueTask<RelayAuthorization> ReauthorizeAsync(GuildTribeBroadcastRelayDto dto,
        CancellationToken ct)
    {
        if (dto.SystemCause is not null)
            return IsAuthorizedSystemBroadcast(dto)
                ? RelayAuthorization.System
                : RelayAuthorization.Rejected("InvalidSystemCause");

        if (dto.SourceCharacterId is not { } sourceCharacterId || sourceCharacterId <= 0)
            return RelayAuthorization.Rejected("MissingSourceCharacter");

        try
        {
            var source = await relaySources.GetAsync(sourceCharacterId, ct).ConfigureAwait(false);
            if (source is null)
                return RelayAuthorization.Rejected("SourceCharacterNotFound");

            if (!string.IsNullOrEmpty(dto.AvatarName) &&
                !string.Equals(dto.AvatarName, source.Name, StringComparison.Ordinal))
                return RelayAuthorization.Rejected("SourceAvatarMismatch");

            return (GuildTribeBroadcastKind)dto.Kind switch
            {
                GuildTribeBroadcastKind.GuildAnnouncement => await ReauthorizeGuildAsync(dto, source.Name, true, ct)
                    .ConfigureAwait(false),
                GuildTribeBroadcastKind.GuildChat => await ReauthorizeGuildAsync(dto, source.Name, false, ct)
                    .ConfigureAwait(false),
                GuildTribeBroadcastKind.TribeAnnouncement => await ReauthorizeTribeAsync(dto, source.Tribe, source.Name,
                    true, ct).ConfigureAwait(false),
                GuildTribeBroadcastKind.TribeAnnouncementScroll => await ReauthorizeTribeAsync(dto, source.Tribe,
                    source.Name, false, ct).ConfigureAwait(false),
                GuildTribeBroadcastKind.WorldChat or
                    GuildTribeBroadcastKind.Whisper or
                    GuildTribeBroadcastKind.PartyChat => RelayAuthorization.ForCharacter(source.Name, source.Tribe),
                GuildTribeBroadcastKind.GlobalAnnouncement => ReauthorizeGlobalAnnouncement(source.Name,
                    source.Tribe, source.AccountGrade),
                _ => RelayAuthorization.Rejected("UnknownKind")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Cross-shard guild/tribe relay {RelayId} source authorization lookup failed; " +
                "leaving the relay unacknowledged for retry", dto.RelayId);
            throw;
        }
    }

    private async ValueTask<RelayAuthorization> ReauthorizeGuildAsync(GuildTribeBroadcastRelayDto dto,
        string sourceAvatarName, bool requiresMasterRole, CancellationToken ct)
    {
        if (dto.SourceCharacterId is not { } sourceCharacterId || dto.GuildId is not { } claimedGuildId)
            return RelayAuthorization.Rejected("MissingGuildIdentity");

        var membership = await guilds.GetByCharacterAsync(sourceCharacterId, ct).ConfigureAwait(false);
        if (membership is null || membership.GuildId != claimedGuildId)
            return RelayAuthorization.Rejected("GuildMembershipMismatch");

        if (requiresMasterRole && !GuildRoleCodec.IsMaster(membership.Role))
            return RelayAuthorization.Rejected("GuildMasterRoleRequired");

        return RelayAuthorization.ForGuild(sourceAvatarName, membership.GuildId);
    }

    private async ValueTask<RelayAuthorization> ReauthorizeTribeAsync(GuildTribeBroadcastRelayDto dto,
        byte sourceTribe, string sourceAvatarName, bool requiresTribeRole, CancellationToken ct)
    {
        if (dto.SourceCharacterId is not { } sourceCharacterId || dto.Tribe is not { } claimedTribe ||
            claimedTribe != sourceTribe)
            return RelayAuthorization.Rejected("TribeMembershipMismatch");

        var authoritativeRole = await tribes.GetRoleForCharacterAsync(sourceCharacterId, ct).ConfigureAwait(false);
        if (requiresTribeRole && authoritativeRole == 0)
            return RelayAuthorization.Rejected("TribeRoleRequired");

        return RelayAuthorization.ForTribe(sourceAvatarName, sourceTribe, authoritativeRole);
    }

    private static RelayAuthorization ReauthorizeGlobalAnnouncement(string sourceAvatarName, byte sourceTribe,
        short sourceAccountGrade)
    {
        return sourceAccountGrade >= (short)GmCommandTier.Basic
            ? RelayAuthorization.ForCharacter(sourceAvatarName, sourceTribe)
            : RelayAuthorization.Rejected("GlobalAnnouncementGmTierRequired");
    }

    private static bool IsAuthorizedSystemBroadcast(GuildTribeBroadcastRelayDto dto)
    {
        return dto is
        {
            Kind: (byte)GuildTribeBroadcastKind.GlobalAnnouncement,
            SourceCharacterId: null,
            SystemCause: (byte)GuildTribeBroadcastSystemCause.WorldNotice,
            GuildId: null,
            Tribe: null,
            RoleField: 0,
            AvatarName: "",
            HasItemLink: false,
            ItemLinkIndex: null,
            ItemLinkActivity: null,
            ItemLinkValue: null,
            ItemLinkSocket0: null,
            ItemLinkSocket1: null,
            ItemLinkSocket2: null
        };
    }

    private void DeliverLocally(GuildTribeBroadcastRelayDto dto, RelayAuthorization authorization)
    {
        switch ((GuildTribeBroadcastKind)dto.Kind)
        {
            case GuildTribeBroadcastKind.GuildAnnouncement:
                DeliverToGuild(authorization.GuildId,
                    new GuildAnnouncementResponse
                        { AvatarName = authorization.SourceAvatarName, Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.GuildChat:
                var link = new ItemLinkInfo
                {
                    Index = dto.ItemLinkIndex ?? 0,
                    Activity = dto.ItemLinkActivity ?? 0,
                    Value = dto.ItemLinkValue ?? 0,
                    Socket = [dto.ItemLinkSocket0 ?? 0, dto.ItemLinkSocket1 ?? 0, dto.ItemLinkSocket2 ?? 0]
                };
                DeliverToGuild(authorization.GuildId,
                    new GuildChatResponse
                        { AvatarName = authorization.SourceAvatarName, Content = dto.Content, Link = link });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncement:
                DeliverToTribe(authorization.Tribe, new TribeAnnouncementResponse
                {
                    TribeRole = authorization.TribeRole, AvatarName = authorization.SourceAvatarName,
                    Content = dto.Content
                });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncementScroll:
                DeliverToTribe(authorization.Tribe, new TribeAnnouncementScrollResponse
                {
                    TribeRole = TribeAnnouncementScrollWireRole,
                    AvatarName = authorization.SourceAvatarName,
                    Content = dto.Content
                });
                break;

            case GuildTribeBroadcastKind.WorldChat:
                DeliverToEveryone(new WorldChatResponse
                {
                    TribeRole = WorldChatWireRole, AvatarName = authorization.SourceAvatarName, Content = dto.Content
                });
                break;

            case GuildTribeBroadcastKind.GlobalAnnouncement:
                DeliverToEveryone(new GlobalAnnouncementResponse { Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.Whisper:
                DeliverWhisper(dto, authorization.SourceAvatarName);
                break;

            case GuildTribeBroadcastKind.PartyChat:
                DeliverPartyChat(dto, authorization.SourceAvatarName);
                break;

            default:
                logger.LogWarning("Relayed broadcast {RelayId} has unrecognized Kind {Kind}; dropped",
                    dto.RelayId, dto.Kind);
                break;
        }
    }

    private void AuditAuthorizationRejection(GuildTribeBroadcastRelayDto dto, string reason)
    {
        logger.LogWarning(
            "Cross-shard guild/tribe relay {RelayId} ({CorrelationId}) from shard {SourceShardId} rejected: " +
            "{Reason}; dropped with no local fan-out", dto.RelayId, dto.CorrelationId, dto.SourceShardId, reason);

        if (eventLogQueue.Enqueue(new EventLogEntryTvp(
                RelayAuthorizationRejectedEventCode,
                (byte)EventLogCategory.AntiCheat,
                null,
                dto.SourceCharacterId is > 0 ? dto.SourceCharacterId : null,
                null,
                null,
                options.Value.ShardId,
                null,
                null,
                null,
                dto.Kind,
                AuthorizationRejectedOutcome,
                $"RelayId={dto.RelayId};CorrelationId={dto.CorrelationId};SourceShardId={dto.SourceShardId};Reason={reason}",
                DateTime.UtcNow)))
            return;

        logger.LogError(
            "game.EventLog write-behind queue full: dropped relay-authorization rejection audit row for relay {RelayId}",
            dto.RelayId);
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

    private void DeliverToEveryone(WorldChatResponse response)
    {
        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            recipient.Session.Send(response);
    }

    private void DeliverToEveryone(GlobalAnnouncementResponse response)
    {
        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
        {
            if (recipient.IsMovingZone)
                continue;

            recipient.Session.Send(response);
        }
    }

    private void DeliverWhisper(GuildTribeBroadcastRelayDto dto, string sourceAvatarName)
    {
        if (dto.GuildId is not { } characterId ||
            !zones.TryGetPlayer(characterId, out var recipient) ||
            recipient.IsMovingZone)
            return;

        recipient.Session.Send(new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = sourceAvatarName,
            Content = dto.Content,
            AuthType = dto.RoleField,
            Link = CreateItemLink(dto)
        });
    }

    private void DeliverPartyChat(GuildTribeBroadcastRelayDto dto, string sourceAvatarName)
    {
        if (dto.GuildId is not { } characterId ||
            !zones.TryGetPlayer(characterId, out var recipient) ||
            recipient.IsMovingZone ||
            !parties.TryResolveMemberByName(characterId, sourceAvatarName, out _))
            return;

        recipient.Session.Send(new PartyChatResponse
        {
            AvatarName = sourceAvatarName,
            Content = dto.Content,
            Link = CreateItemLink(dto)
        });
    }

    private static ItemLinkInfo CreateItemLink(GuildTribeBroadcastRelayDto dto)
    {
        if (!dto.HasItemLink)
            return new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = [0, 0, 0] };

        return new ItemLinkInfo
        {
            Index = dto.ItemLinkIndex ?? 0,
            Activity = dto.ItemLinkActivity ?? 0,
            Value = dto.ItemLinkValue ?? 0,
            Socket = [dto.ItemLinkSocket0 ?? 0, dto.ItemLinkSocket1 ?? 0, dto.ItemLinkSocket2 ?? 0]
        };
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

    protected override void OnOutboxFull(GuildTribeBroadcastRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard guild/tribe broadcast relay outbox full on shard {ShardId}; dropping one {Kind} " +
            "broadcast (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.Kind);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Guild/tribe broadcast outbound flush failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Guild/tribe broadcast inbound delivery failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnPublishFailed(GuildTribeBroadcastRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a {Kind} broadcast to the cross-shard relay from shard {ShardId}; " +
            "cross-shard fan-out for this one message is lost (same-shard delivery already happened)",
            entry.Kind, options.Value.ShardId);
    }

    protected override void OnDeliveryOrAcknowledgementFailed(GuildTribeBroadcastRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to deliver or acknowledge relayed broadcast {RelayId} (kind {Kind}) on shard {ShardId}; " +
            "the durable cursor was not advanced and the relay will be retried",
            dto.RelayId, dto.Kind, options.Value.ShardId);
    }

    private bool IsCompletedCorrelation(Guid correlationId)
    {
        lock (_completedCorrelationGate)
        {
            return _completedCorrelations.Contains(correlationId);
        }
    }

    private void RememberCompletedCorrelation(Guid correlationId)
    {
        lock (_completedCorrelationGate)
        {
            if (!_completedCorrelations.Add(correlationId))
                return;

            _completedCorrelationOrder.Enqueue(correlationId);
            if (_completedCorrelationOrder.Count <= CompletedCorrelationCapacity)
                return;

            _completedCorrelations.Remove(_completedCorrelationOrder.Dequeue());
        }
    }

    private readonly record struct RelayAuthorization(
        bool IsAuthorized,
        string Reason,
        string SourceAvatarName,
        int? GuildId,
        byte? Tribe,
        byte TribeRole)
    {
        public static RelayAuthorization System { get; } = new(true, "", "", null, null, 0);

        public static RelayAuthorization Rejected(string reason)
        {
            return new RelayAuthorization(false, reason, "", null, null, 0);
        }

        public static RelayAuthorization ForCharacter(string sourceAvatarName, byte tribe)
        {
            return new RelayAuthorization(true, "", sourceAvatarName, null, tribe, 0);
        }

        public static RelayAuthorization ForGuild(string sourceAvatarName, int guildId)
        {
            return new RelayAuthorization(true, "", sourceAvatarName, guildId, null, 0);
        }

        public static RelayAuthorization ForTribe(string sourceAvatarName, byte tribe, byte tribeRole)
        {
            return new RelayAuthorization(true, "", sourceAvatarName, null, tribe, tribeRole);
        }
    }
}
