namespace Fenrir.Application.Game.Domain.Commerce;

public static class ProxyShopZonePolicy
{
    public const short ZoneNumber = 37;

    private const float MarketCenterX = 1.0f;

    private const float MarketCenterY = 0.0f;

    private const float MarketCenterZ = -1478.0f;

    private const float MarketRadius = 1000.0f;

    public static bool IsProxyShopZone(short mapId)
    {
        return mapId == ZoneNumber;
    }

    public static bool IsWithinMarketDistrict(float x, float y, float z)
    {
        var dx = x - MarketCenterX;
        var dy = y - MarketCenterY;
        var dz = z - MarketCenterZ;
        return dx * dx + dy * dy + dz * dz < MarketRadius * MarketRadius;
    }
}
