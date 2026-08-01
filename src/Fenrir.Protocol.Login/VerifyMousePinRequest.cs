using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.VerifyMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct VerifyMousePinRequest : IIncomingPacket<VerifyMousePinRequest>
{
    [FixedString(5)] public required string MousePasswordInput { get; init; }
}
