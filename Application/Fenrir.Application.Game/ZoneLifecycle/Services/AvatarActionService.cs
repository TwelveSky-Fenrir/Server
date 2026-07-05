using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>
///     Business logic for CZ_AVATAR_ACTION_SEND (op15) and CZ_UPDATE_AVATAR_ACTION (op16) -- the two opcodes
///     share the same payload/handling (see <c>AvatarActionResumeHandler</c>'s own remarks), so both handlers
///     delegate to this single service.
/// </summary>
public interface IAvatarActionService
{
    void PostAction(Zone zone, int characterId, in ActionInfo action);
}

public sealed class AvatarActionService : IAvatarActionService
{
    public void PostAction(Zone zone, int characterId, in ActionInfo action)
    {
        zone.Post(ZoneCommand.Move(characterId, in action));
    }
}
