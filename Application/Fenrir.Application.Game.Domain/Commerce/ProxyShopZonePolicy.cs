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

    /// <summary>Zone 37's fixed market-district reference point, X axis.</summary>
    private const float MarketCenterX = 1.0f;

    /// <summary>Zone 37's fixed market-district reference point, Y axis.</summary>
    private const float MarketCenterY = 0.0f;

    /// <summary>Zone 37's fixed market-district reference point, Z axis.</summary>
    private const float MarketCenterZ = -1478.0f;

    private const float MarketRadius = 1000.0f;

    public static bool IsProxyShopZone(short mapId)
    {
        return mapId == ZoneNumber;
    }

    /// <summary>
    ///     True only if the given position is strictly inside the zone-37 "market district" -- a 1000-unit-radius
    ///     sphere centered at (1, 0, -1478) -- the in-map geofence legacy applies immediately after the
    ///     zone-number gate, before either a live personal shop or a proxy/deputy shop may open. A position
    ///     exactly on the boundary sphere is rejected, matching legacy's strict less-than comparison. Distance is
    ///     full 3-axis Euclidean, not an XZ-only/flattened shortcut, so a position that is horizontally close to
    ///     the center but far apart on the vertical axis still fails.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:6056-6060 (the single call site, applied identically to
    ///     both the live-shop (<c>tSort==1</c>) and proxy-shop (<c>tSort==2</c>) requests, before their handling
    ///     paths diverge) ; Server/Header/mapcheck.h:189-244 (<c>CheckPossiblePShopRegion</c> -- zone 37's case
    ///     applies no tribe restriction, unlike the four other recognized zone numbers, which are unreachable
    ///     dead code here since Fenrir already restricts this whole feature to zone 37 -- see this type's own
    ///     remarks) ; Server/Header/mapcheck.h:12-15 (<c>GetLengthXYZ</c> -- confirms the distance formula is
    ///     full 3-axis Euclidean, not a flattened variant).
    /// </remarks>
    public static bool IsWithinMarketDistrict(float x, float y, float z)
    {
        var dx = x - MarketCenterX;
        var dy = y - MarketCenterY;
        var dz = z - MarketCenterZ;
        return dx * dx + dy * dy + dz * dz < MarketRadius * MarketRadius;
    }
}
