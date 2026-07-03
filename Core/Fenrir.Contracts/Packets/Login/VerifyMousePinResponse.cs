using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_LOGIN_MOUSE_PASSWORD_RECV (LOGIN.h l.182-186): result of a PIN verification (login protocol report
///     §4.15). 0 = PIN accepted (second-login gate opens), 1 = mismatch (3rd mismatch disconnects instead of
///     replying). No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.VerifyMousePin,
    ExpectedSize = 5)]
public readonly partial record struct VerifyMousePinResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
