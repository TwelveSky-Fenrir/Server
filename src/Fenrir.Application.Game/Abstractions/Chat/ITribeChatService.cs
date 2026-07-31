using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface ITribeChatService
{
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
