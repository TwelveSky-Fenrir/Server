using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IPartyChatService
{

        public bool TrySendChat(PlayerRuntimeState sender, string content);
}
