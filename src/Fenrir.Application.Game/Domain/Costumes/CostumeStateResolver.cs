using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Costumes;

public static class CostumeStateResolver
{
    public enum ResultKind
    {
        NoReply,

        Disconnect,
        Select,
        Equip,
        Remove,

        ReturnToInventoryMismatch,
        ReturnToInventorySuccess
    }

    public const int SlotCount = 10;

    public const int WornMax = 19;

    public static Result Resolve(int sort, int value, in Context ctx)
    {
        switch (sort)
        {
            case 1:
                if (value < 0 || value >= SlotCount || ctx.Wardrobe[value] == 0)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Select, value);

            case 2:
                return new Result(ResultKind.NoReply);

            case 3:
                if (ctx.CostumeIndex < 0 || ctx.CostumeIndex >= SlotCount || ctx.Wardrobe[ctx.CostumeIndex] == 0)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Equip, ctx.CostumeIndex + SlotCount, ctx.Wardrobe[ctx.CostumeIndex]);

            case 4:
                if (ctx.CostumeIndex is < SlotCount or > WornMax || ctx.Wardrobe[ctx.CostumeIndex - SlotCount] == 0)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Remove, ctx.CostumeIndex - SlotCount);

            case 5:
                if (ctx.CostumeIndex != value || ctx.CostumeIndex < 0 || ctx.CostumeIndex >= SlotCount)
                    return new Result(ResultKind.ReturnToInventoryMismatch);

                if (ctx.Wardrobe[value] == 0)
                    return new Result(ResultKind.Disconnect);

                return new Result(ResultKind.ReturnToInventorySuccess, -1, GrantedItemId: ctx.Wardrobe[value],
                    ClearedSlot: value);

            default:
                return new Result(ResultKind.Disconnect);
        }
    }

    public readonly record struct Result(
        ResultKind Kind,
        int NewCostumeIndex = 0,
        int NewCostumeNumber = 0,
        int GrantedItemId = 0,
        int ClearedSlot = -1);

    public readonly record struct Context(int CostumeIndex, ImmutableArray<int> Wardrobe);
}
