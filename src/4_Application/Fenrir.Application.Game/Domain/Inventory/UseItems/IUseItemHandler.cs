using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public interface IUseItemHandler
{
    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context, CancellationToken cancellationToken);
}

public readonly record struct UseItemContext(
    Zone Zone,
    PlayerRuntimeState State,
    int CharacterId,
    int AccountId,
    byte Page,
    byte Index,
    ItemStack Item,
    ItemDefinition Definition,
    int Value);

public static class UseItemResponses
{
    public static UseInventoryItemResponse Fail(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    public static UseInventoryItemResponse InventoryFull(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 3, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    public static UseInventoryItemResponse Success(byte page, byte index, int value = 0, int value2 = 0)
    {
        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = value, Value2 = value2 };
    }
}
