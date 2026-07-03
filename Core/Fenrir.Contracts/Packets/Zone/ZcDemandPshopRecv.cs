using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DEMAND_PSHOP_RECV (ZONE.h:550-554) — reply to CZ_DEMAND_PSHOP_SEND, also used to refresh after
///     each purchase. <c>Result</c> 0 = ok (target's stall); 1 = target not found/changing zone;
///     2 = stall not open. Legacy trap: on error, <c>PshopInfo</c> contains the REQUESTER's own stall
///     (content must be ignored). Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DemandPshopRecv,
    ExpectedSize = 1237)]
public readonly partial record struct ZcDemandPshopRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
