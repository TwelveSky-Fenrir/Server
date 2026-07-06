namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     Which map hosts the offline/deputy ("proxy") personal-shop-stall feature -- both the client-facing
///     opcodes (open/close/withdraw/search/view/update, already gated per-handler to this same number via
///     <c>OpenShopStallHandler.PshopZoneNumber</c>) and <see cref="World.Zone" />'s own periodic rebroadcast
///     sweep.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/mapcheck.h:130-148 (<c>IsValidZoneForProxyShopProcess</c>) -- the shipped
///     non-<c>PPSHOP_V2</c> configuration allowlists four server numbers (1, 6, 11, 140); the alternate,
///     mutually-exclusive <c>PPSHOP_V2</c> configuration instead allowlists exactly one, 37. Fenrir models
///     only the single-number <c>PPSHOP_V2</c> variant (an existing decision, not a new one made by this
///     type -- every proxy-shop handler already hardcoded 37 as its own gate before this policy existed; this
///     type only centralizes that already-established number so <see cref="World.Zone" />'s tick loop, which
///     cannot reference the Handlers project, can see it too).
///     <para>
///         Fenrir shards <see cref="World.Zone" /> instances disjointly by map (<c>admin.ShardMapAssignments</c>
///         + <c>ShardPartitionGuard</c>), so unlike the legacy cluster -- where several zone-server processes
///         could in principle all host the same map and therefore all needed the explicit server-number
///         allowlist to avoid double-processing the shared proxy-shop table -- Fenrir already gets "exactly one
///         process ever iterates this map's shops" for free from that partitioning. No additional
///         config-driven allowlist is layered on top of the map-number check for that reason.
///     </para>
/// </remarks>
public static class ProxyShopZonePolicy
{
    public const short ZoneNumber = 37;

    public static bool IsProxyShopZone(short mapId)
    {
        return mapId == ZoneNumber;
    }
}
