using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

// Legal only at PinRequired: legacy handler Quit()s if a PIN already exists for the account.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateMousePin,
    ExpectedSize = 14, AllowedStates = [(byte)LoginSessionState.PinRequired])]
public readonly partial record struct CreateMousePinRequest : IIncomingPacket<CreateMousePinRequest>
{
    // Exactly 4 ASCII digits + NUL (CheckMousePassword).
    [FixedString(5)]
    public required string MousePassword { get; init; }
}
