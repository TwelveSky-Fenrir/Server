using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_CREATE_MOUSE_PASSWORD_SEND (CLIENT.h l.47-50): first-time mouse-PIN creation, sent by an account that
///     has no PIN yet while the second-login gate is closed (login protocol report §4.13). Legal ONLY in
///     <see cref="LoginSessionState.PinRequired" /> — the legacy handler Quit()s both when the session is not
///     validated and when <c>IsSecondLogin()</c> is already true.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateMousePasswordSend,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ClCreateMousePasswordSend : IIncomingPacket<ClCreateMousePasswordSend>
{
    /// <summary>MAX_MOUSE_PASSWORD_LENGTH=5: exactly 4 ASCII digits + NUL (CheckMousePassword, function.h l.56-71).</summary>
    [FixedString(5)]
    public required string MousePassword { get; init; }
}
