using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IWorldChatService
{
    public WorldChatOutcome TrySendChat(PlayerRuntimeState sender, string content);
}

public enum WorldChatOutcome
{
    LevelTooLow,

    Muted,

    Sent
}
