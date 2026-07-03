using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_CHANGE_MOUSE_PASSWORD_RECV (LOGIN.h l.176-181, shared struct with op 13): result of a PIN change
///     (login protocol report §4.14). 0 = changed (new PIN echoed in clear from the request), 1 = current PIN
///     mismatch ("0000"), 2 = storage failure ("0000"). No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ChangeMousePin,
    ExpectedSize = 10)]
public readonly partial record struct ChangeMousePinResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(5)] public required string MousePassword { get; init; }
}
