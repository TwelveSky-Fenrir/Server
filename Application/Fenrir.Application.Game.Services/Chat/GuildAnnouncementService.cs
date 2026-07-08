using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     Same-shard delivery is synchronous, via <see cref="ZoneRegistry" />, exactly as before. Cross-shard
///     delivery (a guild member connected to a map hosted by a DIFFERENT live shard) is handed off to
///     <see cref="IGuildTribeBroadcastRelayQueue" /> -- see that interface and <c>GuildTribeBroadcastRelayHost</c>'s
///     own remarks for the full cluster-wide fan-out design (Fenrir's SQL-mediated stand-in for legacy's
///     <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> relay uplink).
/// </summary>
public sealed class GuildAnnouncementService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildAnnouncementService> logger) : IGuildAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        if (sender.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(sender.GuildRoleDb))
        {
            // Not a disconnect-worthy gate on its own (legacy silently ignores a non-master sender), but a
            // legitimate client never offers this action to a non-master, so it is still worth a low-noise
            // trace for spotting a modified client probing the gate.
            logger.LogDebug(
                "Character {CharacterId} guild announcement rejected: caller is not the guild master (guild {GuildId})",
                sender.CharacterId, sender.GuildId);
            return false;
        }

        var response = new GuildAnnouncementResponse { AvatarName = sender.Name, Content = content };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GuildAnnouncement,
            options.Value.ShardId,
            guildId,
            null,
            0,
            sender.Name,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null));

        logger.LogInformation(
            "Character {CharacterId} broadcast a guild announcement to guild {GuildId} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, guildId, recipientCount, content.Length);

        return true;
    }
}
