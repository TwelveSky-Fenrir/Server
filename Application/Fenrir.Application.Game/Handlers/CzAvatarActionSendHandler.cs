using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_AVATAR_ACTION_SEND (op15, wire contract §5.7). AllowedStates = [InWorld] on the packet itself
///     guarantees CharacterId is set (MarkTicketConsumed already ran before InWorld is reachable).
/// </summary>
public sealed class CzAvatarActionSendHandler : IInlinePacketHandler<CzAvatarActionSend>
{
    // Zero SQL, zero lock: just forward to the SESSION'S zone's single-writer command channel (architecture
    // reference D-04/§10.1). The zone reference rides on the session (set at registration, re-pointed by
    // handoffs, ADR-0012) -- no per-packet registry lookup, no fixed-zone injection.
    public void Handle(in CzAvatarActionSend packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // InWorld gating guarantees CurrentZone was set during registration; the pattern match is the cheap
        // belt-and-braces for the (benign, documented) staleness window around a handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        zone.Post(ZoneCommand.Move(zoneSession.CharacterId!.Value, in action));
    }
}
