using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IAvatarActionService
{
    public void PostAction(Zone zone, int characterId, in ActionInfo action, bool isResumeAction = false);
}
