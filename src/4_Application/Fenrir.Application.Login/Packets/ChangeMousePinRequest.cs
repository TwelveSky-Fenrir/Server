using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMousePin,
    ExpectedSize = 19, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ChangeMousePinRequest : IIncomingPacket<ChangeMousePinRequest>
{
    [FixedString(5)] public required string MousePassword { get; init; }

    [FixedString(5)] public required string ChangeMousePassword { get; init; }
}
