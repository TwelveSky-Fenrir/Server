namespace Fenrir.Application.Game.Domain.Inventory;

public static class VaultDateNormalization
{
    public static int NormalizeIfExpired(int storedDate, int today)
    {
        return storedDate < today ? 0 : storedDate;
    }
}
