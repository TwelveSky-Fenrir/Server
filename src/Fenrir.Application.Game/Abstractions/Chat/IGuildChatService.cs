using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGuildChatService
{
    public bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
