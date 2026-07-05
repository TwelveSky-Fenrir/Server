using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op23 CL_FAIL_MOVE_ZONE_1_SEND — rolls back to CharSelect, no reply; the stale session ticket needs no explicit
///     revocation (single-use, short TTL).
/// </summary>
public sealed class ZoneTransferFailureHandler : IInlinePacketHandler<ZoneTransferFailureRequest>
{
    public void Handle(in ZoneTransferFailureRequest packet, IPacketSession session)
    {
        ((LoginClientSession)session).MarkCharSelect();
    }
}
