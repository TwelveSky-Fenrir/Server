using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WorldChatService(ZoneRegistry zones) : IWorldChatService
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

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            recipient.Session.Send(response);

        return WorldChatOutcome.Sent;
    }
}
