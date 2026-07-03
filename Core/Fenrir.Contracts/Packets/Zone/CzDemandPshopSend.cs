using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_DEMAND_PSHOP_SEND (CLIENT.h:315-319) — inspect another avatar's open shop stall.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DemandPshopSend, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzDemandPshopSend : IIncomingPacket<CzDemandPshopSend>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
