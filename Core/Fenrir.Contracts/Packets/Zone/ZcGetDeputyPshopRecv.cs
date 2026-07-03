using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GET_DEPUTY_PSHOP_RECV (ZONE.h:1302-1307) — reply to CZ_GET_DEPUTY_PSHOP_SEND via the proxy shop
///     system + ts25extra IPC. Unicast, only emitted by zones where CZ 108 is registered
///     ({1, 6, 11, 140, 37}; 37 effective under <c>PPSHOP_V2</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetDeputyPshopRecv,
    ExpectedSize = 833)]
public readonly partial record struct ZcGetDeputyPshopRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required ProxyShopUserInfo ProxyUser { get; init; }
}
