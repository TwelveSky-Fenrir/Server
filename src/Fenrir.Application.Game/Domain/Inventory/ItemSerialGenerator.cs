namespace Fenrir.Application.Game.Domain.Inventory;

public static class ItemSerialGenerator
{
    public const int UnconditionalItemType = 0;

    public const int UniqueItemType = 2;

    public const int RareItemType = 3;

    public const int EliteItemType = 4;

    public static int Generate(int itemType, DateTimeOffset instant)
    {
        return itemType switch
        {
            UnconditionalItemType or UniqueItemType or RareItemType or EliteItemType => Digest(instant),
            _ => 0
        };
    }

    // Digest horodate ni unique ni monotone : sprintf "%04d%04d" puis atoi vaut haut*10000+bas tant que
    // bas tient sur 4 chiffres, vrai jusqu'a l'an 9881 (Server/Header/function.h:18-25).
    private static int Digest(DateTimeOffset instant)
    {
        var high = instant.Year + instant.Month + instant.Day + instant.Hour + instant.Minute + instant.Second;
        var low = instant.Year + instant.Minute + instant.Second;
        return high * 10000 + low;
    }
}
