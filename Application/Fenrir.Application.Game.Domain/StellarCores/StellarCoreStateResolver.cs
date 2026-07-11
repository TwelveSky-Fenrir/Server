using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.StellarCores;

public static class StellarCoreStateResolver
{
    public enum ResultKind
    {

                NoReply,

        Select,
        Equip,
        Remove,

                ReturnToInventoryMismatch,

                Disconnect,
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
                if (ctx.CoreIndex < 0 || ctx.CoreIndex >= SlotCount || ctx.Wardrobe[ctx.CoreIndex] == 0)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Equip, ctx.CoreIndex + SlotCount, ctx.Wardrobe[ctx.CoreIndex]);

            case 4:
                if (ctx.CoreIndex is < SlotCount or > WornMax || ctx.Wardrobe[ctx.CoreIndex - SlotCount] == 0)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Remove, ctx.CoreIndex - SlotCount);

            case 5:
                if (ctx.CoreIndex != value || ctx.CoreIndex < 0 || ctx.CoreIndex >= SlotCount)
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
        int NewCoreIndex = 0,
        int NewCoreNumber = 0,
        int GrantedItemId = 0,
        int ClearedSlot = -1);

    public readonly record struct Context(int CoreIndex, ImmutableArray<int> Wardrobe);
}
