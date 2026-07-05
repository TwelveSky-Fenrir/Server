using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

// Carries only the toll amount; destination is resolved client-side via the separate
// CZ_DEMAND_ZONE_SERVER_INFO_2 Sort=5 path, not a truncated read.
[FenrirWireType(4)]
public readonly partial record struct TeleportTollData : IFenrirWireType<TeleportTollData>
{
    // Bounds [0, 100000000].
    public required int Money { get; init; }
}
