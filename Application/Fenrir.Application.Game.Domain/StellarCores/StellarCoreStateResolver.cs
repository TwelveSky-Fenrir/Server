using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.StellarCores;

/// <summary>Pure resolver for CZ_STELLAR_STATE_SEND (op153, S04_MyWork02.cpp:15511). No I/O, no Zone dependency.</summary>
/// <remarks>
///     Clone of <see cref="Costumes.CostumeStateResolver" /> -- same Sort 1-5 shape, same 10-slot wardrobe +
///     -1/0-9/10-19 index-offset convention, same <c>IsValidStellarCore</c>-simplified-to-"non-zero" posture
///     (a ~9-entry item-id whitelist, function.h:2267 -- irrelevant today since nothing grants a stellar core
///     yet, see <see cref="World.PlayerRuntimeState.StellarCoreWardrobe" />'s remarks). Case 3's legacy source
///     has a genuine negative-array-index UB bug when <c>aStellarCoreIndex == -1</c> (the default) -- fixed
///     here exactly like <see cref="Costumes.CostumeStateResolver" />'s own case 3, per D8 (fix real UB, not
///     balance).
/// </remarks>
public static class StellarCoreStateResolver
{
    public enum ResultKind
    {
        /// <summary>Legacy silently returns with no reply and no state change.</summary>
        NoReply,

        Select,
        Equip,
        Remove,

        /// <summary>ZC result 1 -- CoreIndex/Value mismatch or out of range. Self-reply, no disconnect.</summary>
        ReturnToInventoryMismatch,

        /// <summary>A real Quit() condition -- the caller must disconnect.</summary>
        Disconnect,
        ReturnToInventorySuccess
    }

    /// <summary>MAX_AVATAR_STELLAR_NUM.</summary>
    public const int SlotCount = 10;

    /// <summary>MAX_AVATAR_STELLAR_NUM2 -- the highest CoreIndex value considered "worn."</summary>
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
