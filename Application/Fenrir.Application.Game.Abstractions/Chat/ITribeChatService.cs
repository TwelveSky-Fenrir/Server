using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface ITribeChatService
{

        public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
