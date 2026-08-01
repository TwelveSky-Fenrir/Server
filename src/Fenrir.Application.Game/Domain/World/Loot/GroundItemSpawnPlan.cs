namespace Fenrir.Application.Game.Domain.World.Loot;

public readonly record struct GroundItemSpawnPlan(
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    float PosX,
    float PosY,
    float PosZ,
    string Owner,
    int DropSort);

public enum GroundItemSpawnEligibility
{
    Eligible,

    UnsupportedItemType,

    InvalidPackedValue
}
