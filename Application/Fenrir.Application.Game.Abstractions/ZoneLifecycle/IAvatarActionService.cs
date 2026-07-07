using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for CZ_AVATAR_ACTION_SEND (op15) and CZ_UPDATE_AVATAR_ACTION (op16) -- the two opcodes
///     share the same wire payload shape (<c>ActionInfo</c>) and the same stand-up/revive-hack pre-check, so
///     both handlers delegate to this single service. They do NOT share the same Sort/Type legality gate
///     applied downstream in <c>Zone.PlayerLifecycle.HandleMove</c> -- see <c>AvatarActionResumeHandler</c>'s
///     own remarks and <see cref="Fenrir.Application.Game.Domain.Movement.AvatarActionResumeWhitelist" />.
/// </summary>
public interface IAvatarActionService
{
    /// <param name="isResumeAction">
    ///     True for CZ_UPDATE_AVATAR_ACTION (op16), false (default) for CZ_AVATAR_ACTION_SEND (op15) --
    ///     threaded through to <c>Zone.PlayerLifecycle.HandleMove</c> so it evaluates the correct,
    ///     opcode-specific Sort/Type legality table.
    /// </param>
    public void PostAction(Zone zone, int characterId, in ActionInfo action, bool isResumeAction = false);
}
