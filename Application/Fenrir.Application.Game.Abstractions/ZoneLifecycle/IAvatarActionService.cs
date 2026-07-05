using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for CZ_AVATAR_ACTION_SEND (op15) and CZ_UPDATE_AVATAR_ACTION (op16) -- the two opcodes
///     share the same payload/handling (see <c>AvatarActionResumeHandler</c>'s own remarks), so both handlers
///     delegate to this single service.
/// </summary>
public interface IAvatarActionService
{
    public void PostAction(Zone zone, int characterId, in ActionInfo action);
}
