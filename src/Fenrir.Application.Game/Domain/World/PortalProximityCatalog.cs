using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World;

public readonly record struct PortalRegistration(float X, float Y, float Z, short DestinationZoneId);

public sealed class PortalProximityCatalog
{
    public static readonly PortalProximityCatalog Empty =
        new(FrozenDictionary<short, ImmutableArray<PortalRegistration>>.Empty);

    private readonly FrozenDictionary<short, ImmutableArray<PortalRegistration>> _portalsByZone;

    public PortalProximityCatalog(FrozenDictionary<short, ImmutableArray<PortalRegistration>> portalsByZone)
    {
        _portalsByZone = portalsByZone;
    }

    // Server/Header/S19_MyZoneMoveInfo.cpp:1250-1254 -- ReturnNextZone walks slots 0..mNextZoneNum-1 and takes the
    // first match, so slot order is the tie-break (zone 38 slots 8/9 share one coordinate). Never sort nor dedupe.
    public static PortalProximityCatalog FromWorldData(WorldDataCache worldData)
    {
        var builder = new Dictionary<short, ImmutableArray<PortalRegistration>>(worldData.ZonesByNumber.Count);

        foreach (var (zoneNumber, zone) in worldData.ZonesByNumber)
        {
            var registrations = ImmutableArray.CreateBuilder<PortalRegistration>(zone.Portals.Length);

            foreach (var portal in zone.Portals)
                if (portal.TargetZoneNumber is { } destination)
                    registrations.Add(new PortalRegistration(portal.TriggerX, portal.TriggerY, portal.TriggerZ,
                        destination));

            if (registrations.Count > 0)
                builder[zoneNumber] = registrations.ToImmutable();
        }

        return new PortalProximityCatalog(builder.ToFrozenDictionary());
    }

    public bool TryGetPortals(short zoneId, out ImmutableArray<PortalRegistration> portals)
    {
        return _portalsByZone.TryGetValue(zoneId, out portals);
    }
}
