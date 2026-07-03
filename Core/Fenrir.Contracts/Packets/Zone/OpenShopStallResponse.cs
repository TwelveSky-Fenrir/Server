using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_START_PSHOP_RECV (ZONE.h:539-543) — reply to CZ_START_PSHOP_SEND. <c>Result</c> 0 = ok;
///     101 = shop already open (proxy case); 102 = proxy active server-side; 103 = ts25extra IPC failed.
///     The returned stall includes PSHOP_INFO's 3 padding bytes. Success is followed by a ZC_AVATAR_ACTION_RECV
///     (ZC 15) broadcast so the stall becomes visible. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.OpenShopStall,
    ExpectedSize = 1237)]
public readonly partial record struct OpenShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
