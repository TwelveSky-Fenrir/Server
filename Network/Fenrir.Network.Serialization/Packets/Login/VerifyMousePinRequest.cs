using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Legal only at PinRequired: legacy handler Quit()s if the session is unvalidated or already PIN-verified.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.VerifyMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct VerifyMousePinRequest : IIncomingPacket<VerifyMousePinRequest>
{
    // 3 mismatches disconnect the session instead of replying.
    [FixedString(5)] public required string MousePasswordInput { get; init; }
}
