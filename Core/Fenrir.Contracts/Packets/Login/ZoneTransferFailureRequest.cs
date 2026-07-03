using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_FAIL_MOVE_ZONE_1_SEND (CLIENT.h l.107-109, header-only): the client failed to reach the zone it was
///     redirected to by op 22 and rolls the login session back to character select (login protocol report §4.23:
///     <c>mMoveZoneResult=FALSE</c> + playuser re-registration, no reply). The legacy handler Quit()s when the
///     session is NOT in transfer, hence <see cref="LoginSessionState.HandoverIssued" /> only.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransferFailure,
    ExpectedSize = 9, AllowedStates = [(byte)LoginSessionState.HandoverIssued])]
public readonly partial record struct ZoneTransferFailureRequest : IIncomingPacket<ZoneTransferFailureRequest>
{
}
