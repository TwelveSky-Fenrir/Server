namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     The five hardcoded server-number/tribe/position regions <c>CheckPossiblePShopRegion</c> recognizes as
///     legal for a LIVE personal-shop-open avatar to remain within -- consumed by
///     <see cref="Simulation.PersonalShopRegionEnforcementSystem" />'s per-tick re-check (behavior contract
///     C21§E: "per-tick personal-shop region re-validation"). Distinct from
///     <see cref="ProxyShopZonePolicy.IsWithinMarketDistrict" />, the one-time OPEN-time gate
///     (<c>OpenShopStallService.PrepareAsync</c>), which Fenrir already simplifies to the single zone-37 case
///     since the live <c>PPSHOP_V2</c> build variant restricts the whole personal/proxy-shop feature to zone
///     37 (see that type's own remarks). This type intentionally reproduces the FULL 5-region table the
///     underlying legacy function recognizes, not just the zone-37 simplification, because the per-tick
///     re-check contract's own citations document all 5 explicitly and this is a direct, narrowly-scoped
///     re-implementation of that specific function -- the other four regions are presently unreachable given
///     Fenrir's zone-37-only shop-open gate, the same "documented, currently-dead branch, kept for legacy
///     parity/future-proofing rather than trimmed" posture <see cref="ProxyShopZonePolicy" />'s own remarks
///     already accept for the inverse direction.
/// </summary>
/// <remarks>
///     Réf. C++ (via behavior contract C21, "Trigger E"/"Edge cases E", not independently re-opened this
///     session -- every coordinate/tribe/radius value below is taken verbatim from that contract, never
///     invented): Server/ts25zone/S07_MyGame04.cpp:333-340 (the per-tick call site); Server/Header/
///     mapcheck.h:189-244, specifically :237 (<c>CheckPossiblePShopRegion</c> -- unconditional failure for any
///     zone number outside the five recognized here).
///     <para>
///         Distance comparison is strict less-than against radius², matching
///         <see cref="ProxyShopZonePolicy.IsWithinMarketDistrict" />'s own strict-less-than treatment of the
///         same underlying legacy function's zone-37 case (a position exactly on the boundary sphere is
///         rejected) -- inferred by analogy across the two call sites of the same C++ function, not
///         independently re-verified against <c>mapcheck.h</c>'s exact comparison operator this session.
///     </para>
/// </remarks>
public static class PersonalShopRegionPolicy
{
    private const float Radius = 1000f;
    private const float RadiusSquared = Radius * Radius;

    private static readonly Region[] Regions =
    [
        new(1, 4f, 0f, -2f, 0),
        new(6, -189f, 0f, 1150f, 1),
        new(11, 449f, 1f, 439f, 2),
        new(140, 452f, 0f, 487f, 3),
        new(37, 1f, 0f, -1478f, null)
    ];

    /// <summary>
    ///     True if <paramref name="mapId" />/<paramref name="tribe" />/<paramref name="x" />,
    ///     <paramref name="y" />,<paramref name="z" /> falls inside one of the five permitted regions. Any
    ///     <paramref name="mapId" /> outside the five recognized here fails unconditionally, matching
    ///     <c>mapcheck.h:237</c>'s own unconditional-failure default case.
    /// </summary>
    public static bool IsWithinPermittedRegion(short mapId, byte tribe, float x, float y, float z)
    {
        foreach (var region in Regions)
        {
            if (region.MapId != mapId)
                continue;

            // Zone 37 applies no tribe restriction (RequiredTribe: null); the other four each require an
            // exact tribe match.
            if (region.RequiredTribe is { } requiredTribe && tribe != requiredTribe)
                return false;

            var dx = x - region.X;
            var dy = y - region.Y;
            var dz = z - region.Z;
            return dx * dx + dy * dy + dz * dz < RadiusSquared;
        }

        return false;
    }

    private readonly record struct Region(short MapId, float X, float Y, float Z, byte? RequiredTribe);
}
