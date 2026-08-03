using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WorldChatService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<WorldChatService> logger) : IWorldChatService
{
    private const int MinimumLevel = 10;

    public WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content)
    {
        if (sender.Level < MinimumLevel)
            return WorldChatOutcome.LevelTooLow;

        if (sender.IsMuted)
            return WorldChatOutcome.Muted;

        var response = new WorldChatResponse
        {
            TribeRole = sender.Tribe,
            AvatarName = sender.Name,
            Content = content
        };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
        {
            recipient.Session.Send(response);
            recipientCount++;
        }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.WorldChat,
            options.Value.ShardId,
            null,
            sender.Tribe,
            sender.Tribe,
            sender.Name,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null));

        logger.LogDebug(
            "Character {CharacterId} broadcast world chat cluster-wide ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, recipientCount, content.Length);

        return WorldChatOutcome.Sent;
    }
}
