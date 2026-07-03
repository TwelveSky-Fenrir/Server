using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_UPDATE_AVATAR_ACTION (op16, wire contract §5.7). Same payload and treatment as CZ_AVATAR_ACTION_SEND
///     (op15) -- the legacy client emits the two opcodes at different moments, but the server-side effect is
///     identical, so this handler is a twin of <see cref="CzAvatarActionSendHandler" />.
///     AllowedStates = [InWorld] on the packet itself guarantees CharacterId is set.
/// </summary>
public sealed class CzUpdateAvatarActionHandler : IInlinePacketHandler<CzUpdateAvatarAction>
{
    // Zero SQL, zero lock: just forward to the SESSION'S zone's single-writer command channel (architecture
    // reference D-04/§10.1) -- same routing rationale as CzAvatarActionSendHandler.
    public void Handle(in CzUpdateAvatarAction packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        zone.Post(ZoneCommand.Move(zoneSession.CharacterId!.Value, in action));
    }
}
