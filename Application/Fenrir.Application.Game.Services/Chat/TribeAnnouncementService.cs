using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     Same-shard delivery is synchronous, via <see cref="ZoneRegistry" />, exactly as before. Cross-shard
///     delivery (a tribe member connected to a map hosted by a DIFFERENT live shard) is handed off to
///     <see cref="IGuildTribeBroadcastRelayQueue" /> -- see that interface and <c>GuildTribeBroadcastRelayHost</c>'s
///     own remarks for the full cluster-wide fan-out design (Fenrir's SQL-mediated stand-in for legacy's
///     <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> relay uplink).
/// </summary>
public sealed class TribeAnnouncementService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<TribeAnnouncementService> logger) : ITribeAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        // Legacy gate is "if (tTribeRole == 0) return;" -- any non-zero role passes: tribe master (1),
        // sub-master (2), or an elected tribe-council member seated via the tribe-vote mechanism (3).
        // Server/ts25zone/S04_MyWork02.cpp:11496-11500; Server/Header/function.h:92-114 (ReturnTribeRole).
        if (sender.TribeRole == 0)
        {
            logger.LogDebug(
                "Character {CharacterId} tribe announcement rejected: caller holds no tribe role (tribe {Tribe})",
                sender.CharacterId, sender.Tribe);
            return false;
        }

        var response = new TribeAnnouncementResponse
            { TribeRole = sender.TribeRole, AvatarName = sender.Name, Content = content };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.TribeAnnouncement,
            options.Value.ShardId,
            GuildId: null,
            Tribe: sender.Tribe,
            RoleField: sender.TribeRole,
            AvatarName: sender.Name,
            Content: content,
            HasItemLink: false,
            ItemLinkIndex: null,
            ItemLinkActivity: null,
            ItemLinkValue: null,
            ItemLinkSocket0: null,
            ItemLinkSocket1: null,
            ItemLinkSocket2: null));

        logger.LogInformation(
            "Character {CharacterId} (tribe role {TribeRole}) broadcast a tribe announcement to tribe {Tribe} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, sender.TribeRole, sender.Tribe, recipientCount, content.Length);

        return true;
    }
}
