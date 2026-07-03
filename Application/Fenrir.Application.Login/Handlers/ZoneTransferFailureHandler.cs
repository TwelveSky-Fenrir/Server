using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op23 CL_FAIL_MOVE_ZONE_1_SEND — the client could not reach the zone it was redirected to by op 22 and
///     rolls back to character select (login protocol report §4.23). Legacy: <c>mMoveZoneResult=FALSE</c> +
///     playuser re-registration, and — crucially — NO reply; the "must be in transfer" precondition (Quit
///     otherwise) is enforced by <c>AllowedStates=[HandoverIssued]</c>. Here the rollback is simply the state
///     transition back to <c>CharSelect</c>, which re-enables ops 17/18/19/21/22/25 so the player can pick a
///     character again. The already-minted session ticket needs no explicit revocation: it is single-use with a
///     short TTL (ADR-0005), and a later successful op 22 simply mints a fresh one.
/// </summary>
public sealed class ZoneTransferFailureHandler : IInlinePacketHandler<ZoneTransferFailureRequest>
{
    public void Handle(in ZoneTransferFailureRequest packet, IPacketSession session)
    {
        ((LoginClientSession)session).MarkCharSelect();
    }
}
