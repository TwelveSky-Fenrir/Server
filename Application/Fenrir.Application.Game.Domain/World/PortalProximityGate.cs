namespace Fenrir.Application.Game.Domain.World;

/// <summary>What <see cref="PortalProximityGate.Evaluate" /> decided for one portal-reason zone-move request.</summary>
public enum PortalProximityOutcome
{
    /// <summary>Not a portal-reason request, no portal data registered for the source zone yet, or a match was found.</summary>
    Allowed,

    /// <summary>A portal-reason request whose declared target has no registered portal within range.</summary>
    RejectedNotNearRegisteredPortal
}

/// <summary>
///     FENRIR-ONLY HARDENING for the portal move-reason (Sort==4) case of a zone-transfer request -- see
///     <see cref="PortalProximityCatalog" />'s own remarks for why this has no direct legacy call site to
///     mirror. The requester's own server-authoritative last-known position (never a client-supplied coordinate
///     -- the live wire packet carries none, contract Preconditions) must be within
///     <see cref="ProximityRadius" /> world units of a portal registered in <see cref="PortalProximityCatalog" />
///     for the source zone whose own registered destination equals the requested target zone.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (mechanism recovered from dead/never-called code; distance formula only):
///         Server/Header/S19_MyZoneMoveInfo.cpp:1260-1278 (the inert routine's own match rule: "distance under
///         30 units AND a legal move-zone transition" -- interpreted here as "the matched portal's own
///         registered destination equals the requested target zone," a Fenrir-authored reading, since the
///         routine was never called on the server and its exact legality predicate was not independently
///         re-verified this session); Server/ts25zone/S07_MyGame03.cpp:5040-5043 (the distance routine legacy
///         would have used is a true 3D Euclidean distance, vertical axis included -- not a horizontal-plane
///         check; reproduced faithfully below even though the gate itself is new).
///     </para>
///     <para>
///         No entry for the source zone (including the shipped <see cref="PortalProximityCatalog.Empty" />) or
///         a non-portal move reason both no-op to <see cref="PortalProximityOutcome.Allowed" /> -- an empty
///         catalog is the correct, deliberate default here, not a bug (see catalog remarks).
///     </para>
///     <para>
///         Open flag carried forward from the contract, not resolved by this gate: severity-classify this trust
///         gap with <c>cpp-security-debt-auditor</c> before broadening this mechanism further (e.g. extending it
///         to other move-reason codes, or replacing the client-declared target with a fully server-derived one)
///         -- this implementation only closes the one case (Sort==4, validating the client's declared target
///         against real portal data once such data exists) the contract calls out by name.
///     </para>
/// </remarks>
public static class PortalProximityGate
{
    /// <summary>
    ///     The one move-reason (Sort) code this gate reacts to -- every other reason is unaffected (contract
    ///     Inputs). <c>int</c> to match the wire packet's own <c>ZoneMoveRequest.Sort</c> field type directly,
    ///     no narrowing cast required at the call site.
    /// </summary>
    public const int PortalMoveReasonSort = 4;

    /// <summary>30 world units, a full 3D Euclidean radius (S07_MyGame03.cpp:5040-5043) -- "distance UNDER 30 units."</summary>
    public const float ProximityRadius = 30f;

    private const float ProximityRadiusSquared = ProximityRadius * ProximityRadius;

    public static PortalProximityOutcome Evaluate(
        PortalProximityCatalog catalog,
        short sourceZoneId,
        float requesterX,
        float requesterY,
        float requesterZ,
        int moveReasonSort,
        short targetZoneNumber)
    {
        if (moveReasonSort != PortalMoveReasonSort)
            return PortalProximityOutcome.Allowed;

        if (!catalog.TryGetPortals(sourceZoneId, out var portals))
            return PortalProximityOutcome.Allowed; // no data registered for this zone yet -- documented no-op

        foreach (var portal in portals)
        {
            if (portal.DestinationZoneId != targetZoneNumber)
                continue;

            var dx = portal.X - requesterX;
            var dy = portal.Y - requesterY;
            var dz = portal.Z - requesterZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return PortalProximityOutcome.Allowed;
        }

        return PortalProximityOutcome.RejectedNotNearRegisteredPortal;
    }
}
