using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_MAKE_ITEM2_SEND (CLIENT.h:289) — same typedef as <see cref="CraftItemRequest" /> (29). Guarded by
///     <c>if (tSort != 2) { Quit(); return; }</c> before the switch: only <c>tSort</c> 2 is reachable
///     (legendary pets: 10,000 CP, materials 1878/2150x2); cases 0/1/3 are dead code. Response: ZC 168.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CraftLegendaryPet, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftLegendaryPetRequest : IIncomingPacket<CraftLegendaryPetRequest>
{
    public required int Sort { get; init; }

    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Page3 { get; init; }

    public required int Index3 { get; init; }

    public required int Page4 { get; init; }

    public required int Index4 { get; init; }
}
