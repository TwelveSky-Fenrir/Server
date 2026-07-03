using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TempRegisterSend, ExpectedSize = 272,
    AllowedStates = [(byte)ZoneSessionState.Connected])]
public readonly partial record struct CzTempRegisterSend : IIncomingPacket<CzTempRegisterSend>
{
    // XOR USE_XOR_UID: de-obfuscated by the handler/Network layer, not by TryRead here.
    [FixedString(255)] public required string Id { get; init; }
    public required int Tribe { get; init; }
    public required int UserSort { get; init; }
}
