using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op24 CL_CHANGE_MASTER_SEND — faithful no-op (plan decision D1, "enregistré-mais-mort" reproduced):
///     W_CHANGE_MASTER_SEND is a 100% empty body in the legacy (S04_MyWork02.cpp l.1655-1658) — it reads
///     nothing, never replies (LCP 27 CHANGE_MASTER_RECV is never emitted anywhere), never Quit()s, checks no
///     state. The teacher/master feature was ripped out without cleanup. This handler exists so the packet is
///     consumed silently instead of tripping the dispatcher's "no handler registered" warning on every send.
/// </summary>
public sealed class ChangeMasterHandler : IInlinePacketHandler<ChangeMasterRequest>
{
    public void Handle(in ChangeMasterRequest packet, IPacketSession session)
    {
        // Intentionally empty — see the type doc. Never add a reply here: the legacy client has no LCP 27 path.
    }
}
