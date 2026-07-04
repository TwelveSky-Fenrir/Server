using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateMousePin,
    ExpectedSize = 10)]
public readonly partial record struct CreateMousePinResponse : IOutgoingPacket
{
    // 0 = created; failure Quit()s instead of replying (failure code is dead in the legacy source).
    public required int Result { get; init; }

    // Echoed PIN comes from the request, not storage -- server-side the PIN only ever exists hashed.
    [FixedString(5)] public required string MousePassword { get; init; }
}
