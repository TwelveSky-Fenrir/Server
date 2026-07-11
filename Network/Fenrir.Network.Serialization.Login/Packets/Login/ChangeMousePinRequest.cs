using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMousePin,
    ExpectedSize = 19, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ChangeMousePinRequest : IIncomingPacket<ChangeMousePinRequest>
{
    [FixedString(5)] public required string MousePassword { get; init; }

    [FixedString(5)] public required string ChangeMousePassword { get; init; }
}
