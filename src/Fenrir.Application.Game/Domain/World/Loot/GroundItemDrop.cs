namespace Fenrir.Application.Game.Domain.World.Loot;

public readonly record struct GroundItemReference(int ItemId, int ItemType);

public readonly record struct GroundItemSocketGems(int First, int Second, int Third);

public readonly record struct GroundItemState(byte Enchant, byte Combine, byte Refine, byte Socket)
{
    public int PackedValue => ItemValueCodec.Encode(Enchant, Combine, Refine, Socket);
}

public readonly record struct GroundItemReplicationState(
    GroundItemState State,
    GroundItemSocketGems SocketGems,
    int ExpireDate)
{
    public int PackedValue => State.PackedValue;
}

public readonly record struct GroundItemDropRequest(
    GroundItemReference Item,
    int Quantity,
    GroundItemOrigin Origin,
    GroundItemReplicationState Replication);

public readonly record struct GroundItemDrop(
    GroundItemReference Item,
    int Quantity,
    int SerialNumber,
    GroundItemOrigin Origin,
    GroundItemReplicationState Replication);

public enum GroundItemOrigin
{
    Monster,
    PlayerInventory,
    PlayerVersusPlayer,
    GameMaster,
    Reward,
    ScriptedEvent
}
