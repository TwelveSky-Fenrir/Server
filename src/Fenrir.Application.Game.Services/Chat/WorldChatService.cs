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
    private const byte WorldChatWireRole = 1;

    public WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content)
    {
        if (sender.IsMuted)
            return WorldChatOutcome.Muted;

        if (sender.Level < MinimumLevel)
            return WorldChatOutcome.LevelTooLow;

        var response = new WorldChatResponse
        {
            TribeRole = WorldChatWireRole,
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
            WorldChatWireRole,
            sender.Name,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null)
        {
            SourceCharacterId = sender.CharacterId
        });

        logger.LogDebug(
            "Character {CharacterId} broadcast world chat cluster-wide ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, recipientCount, content.Length);

        return WorldChatOutcome.Sent;
    }
}
