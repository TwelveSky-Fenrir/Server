using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateMousePin,
    ExpectedSize = 10)]
public readonly partial record struct CreateMousePinResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(5)] public required string MousePassword { get; init; }
}
