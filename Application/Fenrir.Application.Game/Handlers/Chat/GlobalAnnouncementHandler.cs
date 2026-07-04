using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_NOTICE_SEND (opcode 17) -- GM global announcement. The legacy gate is
///     <c>uUserSort &lt; 1 =&gt; silent return</c> (non-GM senders are simply ignored, no error, no
///     relay). OPEN ISSUE (deliberate, documented): Fenrir has NO GM-rank concept anywhere in its data
///     model yet (<c>ZoneHandshakeRequest.UserSort</c> is read off the wire but never persisted/trusted --
///     see that handler's own remarks; trusting a client-supplied rank would be a privilege-escalation
///     hole regardless). Every sender is therefore treated as rank 0 -- this handler is registered (so the
///     opcode round-trips correctly) but PERMANENTLY inert until a real, server-authoritative GM-rank
///     source exists. This is the one "notice" channel this pass could not wire for real, unlike
///     Guild/Tribe announcement (whose master/sub-master gates ARE real, DB-backed facts).
/// </summary>
public sealed class GlobalAnnouncementHandler : IInlinePacketHandler<GlobalAnnouncementRequest>
{
    public void Handle(in GlobalAnnouncementRequest packet, IPacketSession session)
    {
        // Every character is rank 0 today -- see class remarks. Intentionally a permanent no-op, not a
        // TODO: the moment a real GM-rank source lands, this is the one line to change.
    }
}
