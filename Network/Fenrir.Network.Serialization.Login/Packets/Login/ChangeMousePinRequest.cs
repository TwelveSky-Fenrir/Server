using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

// Legal only at PinRequired: legacy handler Quit()s if the session already passed the PIN gate.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMousePin,
    ExpectedSize = 19, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly record struct ChangeMousePinRequest : IIncomingPacket<ChangeMousePinRequest>
{
    // 3 mismatches disconnect the session instead of replying.
    [FixedString(5)] public required string MousePassword { get; init; }

    [FixedString(5)] public required string ChangeMousePassword { get; init; }
}
