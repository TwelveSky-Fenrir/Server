using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>One registered portal's location within its owning (source) zone and the destination it leads to.</summary>
public readonly record struct PortalRegistration(float X, float Y, float Z, short DestinationZoneId);

/// <summary>
///     Per-zone table of registered portal coordinates, backing <see cref="PortalProximityGate" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>FENRIR-ONLY HARDENING, NOT LEGACY PARITY.</b> Contract A8-portal-warzone's own Edge cases section
///         raises this explicitly: the legacy 30-unit portal proximity check (<c>ReturnNextZone</c>,
///         Server/Header/S19_MyZoneMoveInfo.cpp:1260) is COMPILED but has NO call site anywhere under
///         <c>Server/</c> -- the live portal move-reason code (Sort==4) passes through the zone-transfer handler
///         with zero positional validation (Server/ts25zone/S04_MyWork02.cpp:2086-2087). The contract flags
///         reproducing that gap as unacceptable ("this is a server-authority/trust gap... do not silently 'fix'
///         it as parity") and asks that it be treated as a deliberate hardening decision, not a legacy-parity
///         implementation. This catalog exists to let Fenrir close that gap on purpose, going beyond what legacy
///         ever actually enforced -- see <see cref="PortalProximityGate" /> for the check itself.
///     </para>
///     <para>
///         <see cref="Empty" /> is the shipped default: the contract's own Preconditions section notes that a
///         real per-zone portal coordinate table exists in legacy (filled during zone-move initialization) but
///         does not cite the actual coordinates, so inventing them here is out of the question. Same
///         "documented always-allow until real data lands" posture <see cref="ZoneWar.TribeGuardCorridorCatalog.Empty" />
///         already establishes for this codebase: once a real per-zone portal coordinate table is gathered (a
///         data-collection task, not a code change) and supplied here, the gate activates for every zone with an
///         entry, with no further change required at any call site.
///     </para>
/// </remarks>
public sealed class PortalProximityCatalog
{
    public static readonly PortalProximityCatalog Empty =
        new(ImmutableDictionary<short, ImmutableArray<PortalRegistration>>.Empty);

    private readonly ImmutableDictionary<short, ImmutableArray<PortalRegistration>> _portalsByZone;

    public PortalProximityCatalog(ImmutableDictionary<short, ImmutableArray<PortalRegistration>> portalsByZone)
    {
        _portalsByZone = portalsByZone;
    }

    /// <summary>True if <paramref name="zoneId" /> has at least one registered portal, with that zone's own list.</summary>
    public bool TryGetPortals(short zoneId, out ImmutableArray<PortalRegistration> portals)
    {
        return _portalsByZone.TryGetValue(zoneId, out portals);
    }
}
