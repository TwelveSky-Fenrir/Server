using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

// Legal only at PinRequired: legacy handler Quit()s if the session already passed the PIN gate.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMousePin,
    ExpectedSize = 19, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct ChangeMousePinRequest : IIncomingPacket<ChangeMousePinRequest>
{
    // 3 mismatches disconnect the session instead of replying.
    [FixedString(5)]
    public required string MousePassword { get; init; }

    [FixedString(5)]
    public required string ChangeMousePassword { get; init; }
}
