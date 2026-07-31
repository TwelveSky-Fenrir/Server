using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public readonly record struct PortalRegistration(float X, float Y, float Z, short DestinationZoneId);

public sealed class PortalProximityCatalog
{
    public static readonly PortalProximityCatalog Empty =
        new(ImmutableDictionary<short, ImmutableArray<PortalRegistration>>.Empty);

    private readonly ImmutableDictionary<short, ImmutableArray<PortalRegistration>> _portalsByZone;

    public PortalProximityCatalog(ImmutableDictionary<short, ImmutableArray<PortalRegistration>> portalsByZone)
    {
        _portalsByZone = portalsByZone;
    }

    public bool TryGetPortals(short zoneId, out ImmutableArray<PortalRegistration> portals)
    {
        return _portalsByZone.TryGetValue(zoneId, out portals);
    }
}
