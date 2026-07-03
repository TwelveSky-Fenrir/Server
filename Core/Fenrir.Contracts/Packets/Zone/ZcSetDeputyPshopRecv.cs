using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SET_DEPUTY_PSHOP_RECV (ZONE.h:1309-1317) — reply to CZ_SET_DEPUTY_PSHOP_SEND. The builder
///     merges <c>tValue[6]</c> (item) + <c>tSocket[3]</c> into <see cref="Value1" /> (9 ints: 6 item
///     values then 3 sockets). <c>Page</c>/<c>Index</c> = the affected player inventory slot. Unicast;
///     same zone restriction as ZC_GET_DEPUTY_PSHOP_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetDeputyPshopRecv,
    ExpectedSize = 877)]
public readonly partial record struct ZcSetDeputyPshopRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required ProxyShopUserInfo ProxyUser { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(9)] public required int[] Value1 { get; init; }
    public required int Money { get; init; }
}
