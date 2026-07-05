using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Result: 0=accepted (second-login gate opens), 1=mismatch (3rd mismatch disconnects instead of replying).
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.VerifyMousePin,
    ExpectedSize = 5)]
public readonly partial record struct VerifyMousePinResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
