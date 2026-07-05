using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class AvatarActionService : IAvatarActionService
{
    public void PostAction(Zone zone, int characterId, in ActionInfo action)
    {
        zone.Post(ZoneCommand.Move(characterId, in action));
    }
}
