using System.Collections.Immutable;

namespace Fenrir.Application.Game.Costumes;

/// <summary>Pure resolver for CZ_COSTUME_STATE_SEND (op90) Sort 1-5. No I/O, no Zone dependency.</summary>
/// <remarks>
///     <see cref="CostumeStateContext.Wardrobe" /> occupancy is simplified to "non-zero" rather than the
///     legacy's ~300-entry <c>IsValidCostume</c> item-id whitelist (Server/Header/function.h) -- see
///     <c>PlayerRuntimeState.CostumeWardrobe</c>'s own remarks. Since nothing grants a costume yet, every slot
///     is always 0 today, so Select/Equip/Remove/ReturnToInventorySuccess never actually fire outside a test
///     that seeds the wardrobe directly.
/// </remarks>
public static class CostumeStateResolver
{
    public enum ResultKind
    {
        /// <summary>Legacy silently returns with no reply and no state change.</summary>
        NoReply,

        /// <summary>A real Quit() condition -- the caller must disconnect.</summary>
        Disconnect,
        Select,
        Equip,
        Remove,

        /// <summary>ZC result 1 -- CostumeIndex/Value mismatch or out of range. Self-reply, no disconnect.</summary>
        ReturnToInventoryMismatch,
        ReturnToInventorySuccess
    }

    /// <summary>MAX_AVATAR_COSTUME_NUM.</summary>
    public const int SlotCount = 10;

    /// <summary>MAX_AVATAR_COSTUME_NUM2 -- the highest CostumeIndex value considered "worn."</summary>
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
