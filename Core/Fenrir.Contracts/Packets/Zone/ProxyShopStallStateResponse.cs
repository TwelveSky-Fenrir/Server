using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DEPUTY_PSHOP_ACTION_RECV (ZONE.h:1294-1300) — spawn/despawn/state of a world-visible deputy
///     (proxy) shop stall. Proximity broadcast (<c>Broadcast11</c>), emitted by any zone hosting proxy
///     shops (in practice zone 37 in EU33, cf. CZ_GET_DEPUTY_PSHOP_SEND). The sole client-facing use of
///     <see cref="ProxyStateInfo" />: its 2 trailing padding bytes (C++ <c>sizeof</c> 52 vs. the 50-byte
///     wire type) are carried here via <c>[Reserved(2)]</c> on <see cref="CheckChangeActionState" />.
///     <c>UniqueNumber</c> is a plain <c>int</c>, not <c>DWORD</c>. <c>CheckChangeActionState</c>: 1 =
///     spawn/state update, 0 = periodic re-broadcast without change (client skips replaying the animation).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProxyShopStallState,
    ExpectedSize = 65)]
public readonly partial record struct ProxyShopStallStateResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required int UniqueNumber { get; init; }
    public required ProxyStateInfo ProxyObject { get; init; }
    [Reserved(2)] public required int CheckChangeActionState { get; init; }
}
