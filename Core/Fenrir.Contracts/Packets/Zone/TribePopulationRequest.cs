using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GET_ZONE_CONNECT_USER_SEND (CLIENT.h:357-360, <c>S_GET_ZONE_CONNECT_USER_SEND</c>
///     CLIENT.h:673) — custom handler <c>W_GET_ZONE_CONNECT_USER_SEND</c> (S04_MyWork02.cpp:12817)
///     IGNORES <see cref="ZoneNumber" /> (<c>(void)r</c>): it counts connected/ready players per tribe
///     (0..3) of the current process and replies with 4 consecutive ZC 111 (tribe slots 348/349/350/351).
///     No validation, no <c>Quit()</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribePopulation,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribePopulationRequest : IIncomingPacket<TribePopulationRequest>
{
    public required int ZoneNumber { get; init; }
}
