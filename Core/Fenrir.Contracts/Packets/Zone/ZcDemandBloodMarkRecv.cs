using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DEMAND_BLOOD_MARK_RECV (ZONE.h:1366-1369) — reply to CZ_DEMAND_BLOOD_MARK_SEND, the whole
///     blood-mark catalog. Emitted under <c>USE_BLOOD</c> (on in EU33). Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DemandBloodMarkRecv,
    ExpectedSize = 605)]
public readonly partial record struct ZcDemandBloodMarkRecv : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
