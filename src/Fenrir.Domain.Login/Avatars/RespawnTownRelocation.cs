namespace Fenrir.Domain.Login.Avatars;

public static class RespawnTownRelocation
{
    public static bool RequiresRelocation(byte avatarTribe, byte owningTribeOfLoggedOutZone)
    {
        return owningTribeOfLoggedOutZone != avatarTribe;
    }
}
