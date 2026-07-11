using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.VerifyMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct VerifyMousePinRequest : IIncomingPacket<VerifyMousePinRequest>
{
    [FixedString(5)] public required string MousePasswordInput { get; init; }
}
