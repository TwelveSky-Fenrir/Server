using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_CHANGE_MOUSE_PASSWORD_SEND (CLIENT.h l.52-56): change the mouse PIN by presenting the current one
///     (login protocol report §4.14). Only legal at the PIN screen: the legacy handler Quit()s when
///     <c>IsSecondLogin()</c> is already true, so a validated session can never change its PIN mid-charselect —
///     hence <see cref="LoginSessionState.PinRequired" /> only, exactly like ops 13/15.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMousePin,
    ExpectedSize = 19, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ChangeMousePinRequest : IIncomingPacket<ChangeMousePinRequest>
{
    /// <summary>The current PIN (verified before the change; 3 mismatches disconnect, §4.14).</summary>
    [FixedString(5)]
    public required string MousePassword { get; init; }

    /// <summary>The replacement PIN (same 4-ASCII-digit format).</summary>
    [FixedString(5)]
    public required string ChangeMousePassword { get; init; }
}
