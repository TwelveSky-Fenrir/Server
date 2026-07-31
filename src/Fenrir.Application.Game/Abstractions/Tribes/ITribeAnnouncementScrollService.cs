using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public interface ITribeAnnouncementScrollService
{
    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content);
}
