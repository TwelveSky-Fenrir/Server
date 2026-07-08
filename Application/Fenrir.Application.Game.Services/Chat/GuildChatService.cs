using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
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
public sealed class GuildChatService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildChatService> logger) : IGuildChatService
{
    public bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.GuildId is not { } guildId)
        {
            logger.LogDebug("Character {CharacterId} guild chat dropped: caller has no guild", sender.CharacterId);
            return false;
        }

        if (sender.IsMuted)
        {
            // A moderation action (mute) actively being enforced -- worth surfacing by default rather than
            // burying it at Debug, so an operator can confirm a mute is actually taking effect.
            logger.LogInformation("Character {CharacterId} guild chat dropped: caller is muted", sender.CharacterId);
            return false;
        }

        var response = new GuildChatResponse { AvatarName = sender.Name, Content = content, Link = link };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GuildChat,
            options.Value.ShardId,
            guildId,
            null,
            0,
            sender.Name,
            content,
            true,
            link.Index,
            link.Activity,
            link.Value,
            // Socket is a fixed 3-element array on the wire ([FixedArray(3)]) -- always present, never
            // shorter, so direct indexing is safe.
            link.Socket[0],
            link.Socket[1],
            link.Socket[2]));

        // Debug, not Information: routine per-message chatter, matching how WhisperHandler's own successful
        // delivery path stays silent beyond PacketLog's packet-sent trace -- content itself is never logged.
        logger.LogDebug(
            "Character {CharacterId} sent guild chat to guild {GuildId} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, guildId, recipientCount, content.Length);

        return true;
    }
}
