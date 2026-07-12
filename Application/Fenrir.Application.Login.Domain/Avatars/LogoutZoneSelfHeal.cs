namespace Fenrir.Application.Login.Domain.Avatars;

public static class ZoneTribeOwnershipCatalog
{
    public static bool BelongsToTribe(short mapId, byte tribe)
    {
        return tribe switch
        {
            0 => mapId is (>= 1 and <= 4) or (>= 16 and <= 18) or 40 or 43 or 46 or 56 or 59 or 62 or 63 or 64 or 71,
            1 => mapId is (>= 6 and <= 9) or (>= 22 and <= 24) or 41 or 44 or 47 or 57 or 60 or 65 or 66 or 67 or 72,
            2 => mapId is (>= 11 and <= 14) or (>= 28 and <= 30) or 42 or 45 or 48 or 58 or 61 or 68 or 69 or 70 or 73,
            3 => mapId is >= 140 and <= 143,
            _ => false
        };
    }
}

public static class LogoutZoneSelfHeal
{
    public static (short MapId, float PosX, float PosY, float PosZ) Apply(byte tribe, short mapId, float posX,
        float posY, float posZ)
    {
        if (ZoneTribeOwnershipCatalog.BelongsToTribe(mapId, tribe))
            return (mapId, posX, posY, posZ);

        return RespawnTownCatalog.TryGetTownLocation(tribe, out var townZone, out var townX, out var townY,
            out var townZ)
            ? (townZone, townX, townY, townZ)
            : (mapId, posX, posY, posZ);
    }
}
