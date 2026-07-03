using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_LOGIN_MOUSE_PASSWORD_SEND (CLIENT.h l.58-61): mouse-PIN verification for an account that already has
///     one (login protocol report §4.15). Legal ONLY in <see cref="LoginSessionState.PinRequired" /> — the
///     legacy handler Quit()s when the session is not validated or when the PIN was already validated.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.LoginMousePasswordSend,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ClLoginMousePasswordSend : IIncomingPacket<ClLoginMousePasswordSend>
{
    /// <summary>The PIN attempt (3 mismatches disconnect, GL_504 in the legacy log, §4.15).</summary>
    [FixedString(5)]
    public required string MousePasswordInput { get; init; }
}
