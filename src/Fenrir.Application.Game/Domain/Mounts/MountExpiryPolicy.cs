namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountExpiryPolicy
{
    public static bool IsExpiredWhileMounted(int animalIndex, int animalTime)
    {
        return animalIndex >= MountStateResolver.SlotCount && animalTime < 1;
    }

    public static int Dismounted(int animalIndex)
    {
        return animalIndex - MountStateResolver.SlotCount;
    }
}
