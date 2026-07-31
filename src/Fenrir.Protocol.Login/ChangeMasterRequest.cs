using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

// Tous les etats, DELIBEREMENT et non par omission. Le handler est un no-op qui ne lit que SessionId,
// donc il n'a aucune hypothese d'etat a proteger, et un refus du gate n'est pas un rejet silencieux :
// SessionLoop appelle Abort(StateViolation). Gater cet opcode ne pourrait donc que tuer des sessions de
// clients legacy qui l'envoient hors sequence, sans rien securiser.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62,
    AllowedStates =
    [
        (byte)LoginSessionState.Connected, (byte)LoginSessionState.VersionOk,
        (byte)LoginSessionState.Authenticated, (byte)LoginSessionState.PinRequired,
        (byte)LoginSessionState.CharSelect, (byte)LoginSessionState.HandoverIssued
    ])]
public readonly partial record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(49)] public required string MasterId { get; init; }
}
