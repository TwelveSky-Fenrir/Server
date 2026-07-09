using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

// Legal only at PinRequired: legacy handler Quit()s if a PIN already exists for the account.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly record struct CreateMousePinRequest : IIncomingPacket<CreateMousePinRequest>
{
    // Exactly 4 ASCII digits + NUL (CheckMousePassword).
    [FixedString(5)] public required string MousePassword { get; init; }
}
