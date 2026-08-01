namespace Fenrir.Domain.Login.Avatars;

public static class RespawnTownCatalog
{
    private const int TribeCount = 4;

    private static readonly (short ZoneId, float X, float Y, float Z)[] LocationByTribe =
    [
        (1, 6f, 0f, -7f),
        (6, -190f, 0f, 1270f),
        (11, 447f, 1f, 440f),
        (140, 0f, 0f, -6f)
    ];

    public static bool TryGetTownLocation(byte tribe, out short zoneId, out float x, out float y, out float z)
    {
        if (tribe < TribeCount)
        {
            (zoneId, x, y, z) = LocationByTribe[tribe];
            return true;
        }

        zoneId = 0;
        x = 0f;
        y = 0f;
        z = 0f;
        return false;
    }
}
