namespace Fenrir.Application.Game.Domain.Commerce;

public sealed class ProxyShopBroadcastEntry(
    int characterId,
    int uniqueNumber,
    string ownerName,
    string shopName,
    float posX,
    float posY,
    float posZ,
    int shopDate)
{
    public int CharacterId { get; } = characterId;

        public int UniqueNumber { get; } = uniqueNumber;

        public string OwnerName { get; } = ownerName;

        public string ShopName { get; } = shopName;

    public float PosX { get; } = posX;
    public float PosY { get; } = posY;
    public float PosZ { get; } = posZ;

        public int ShopDate { get; set; } = shopDate;

        public TimeSpan LastBroadcastAt { get; set; }
}
