using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_CREATE_MOUSE_PASSWORD_RECV (LOGIN.h l.176-181, shared struct with op 14): result of a first-time PIN
///     creation. The legacy server echoes the accepted PIN IN CLEAR (login protocol report §4.13) — the echo
///     comes from the client's own request, never from storage (server-side the PIN only ever exists hashed).
///     No XOR (report §5, table).
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateMousePasswordRecv,
    ExpectedSize = 10)]
public readonly partial record struct LcCreateMousePasswordRecv : IOutgoingPacket
{
    /// <summary>0 = created. The failure reply (1) is commented out in the legacy source — failure Quit()s instead.</summary>
    public required int Result { get; init; }

    /// <summary>The PIN just accepted, echoed in clear from the request ("0000" on the never-taken failure path).</summary>
    [FixedString(5)]
    public required string MousePassword { get; init; }
}
