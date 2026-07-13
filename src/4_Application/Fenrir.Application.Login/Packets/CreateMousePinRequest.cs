using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct CreateMousePinRequest : IIncomingPacket<CreateMousePinRequest>
{
    [FixedString(5)] public required string MousePassword { get; init; }
}
