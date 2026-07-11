using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface ILocalChatService
{
    public bool TryPostChat(Zone zone, ZoneClientSession zoneSession, PlayerRuntimeState sender, string content,
        ItemLinkInfo link);
}
