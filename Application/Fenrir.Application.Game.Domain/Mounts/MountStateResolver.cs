using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     Pure resolver for CZ_ANIMAL_STATE_SEND (op87) Sort 1-4 (Select/Deselect/Mount/Dismount). No I/O, no Zone
///     dependency.
/// </summary>
/// <remarks>
///     Sort 5 (Delete Mount) and 6-8/120 (attribute convert/delete/transfer/tier-upgrade) are out of scope: they
///     need per-slot <c>aAnimalPower</c>/<c>aAnimalExpActivity</c> arrays and probability tables Fenrir doesn't
///     catalog yet -- same posture as <c>CraftResolver</c>'s own gacha/recipe scope cut. Sort 5 additionally has
///     a verified quirk vs the C++ source worth flagging rather than silently reproducing: it searches
///     <c>aAnimal[10]</c> for a slot whose VALUE equals the request's <c>tValue</c>, so requesting deletion of
///     "mount id 0" always matches the first empty slot and grants +250 CP for free, with no real mount ever
///     required. Since <see cref="MountStateContext.Garage" /> has no acquisition path yet (always all-zero),
///     that quirk is the ONLY way Sort 5 could ever succeed today -- treating Sort 5+ as unsupported closes that
///     surface without touching any reachable, legitimate behavior.
/// </remarks>
public static class MountStateResolver
{
    public enum ResultKind
    {
        /// <summary>Legacy silently returns with no reply and no state change.</summary>
        NoReply,

        /// <summary>A real Quit() condition -- the caller must disconnect.</summary>
        Disconnect,
        Select,
        Deselect,
        Mount,
        Dismount
    }

    /// <summary>MAX_AVATAR_ANIMAL_NUM.</summary>
    public const int SlotCount = 10;

    /// <summary>MAX_AVATAR_ANIMAL_NUM2 -- the highest AnimalIndex value considered "mounted."</summary>
    public const int MountedMax = 19;

    public static Result Resolve(int sort, int value, in Context ctx)
    {
        switch (sort)
        {
            case 1:
                return value is < 0 or >= SlotCount
                    ? new Result(ResultKind.NoReply)
                    : new Result(ResultKind.Select, value);

            case 2:
                if (value < 0 || value >= SlotCount || ctx.AnimalIndex >= SlotCount)
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Deselect, -1);

            case 3:
                if (ctx.AnimalIndex < 0 || ctx.AnimalIndex >= SlotCount || ctx.AnimalTime < 1 || ctx.ActionSort != 1)
                    return new Result(ResultKind.NoReply);

                var mountedIndex = ctx.AnimalIndex + SlotCount;
                var animalNumber = ctx.Garage[ctx.AnimalIndex];
                return new Result(ResultKind.Mount, mountedIndex, animalNumber);

            case 4:
                return ctx.AnimalIndex is < SlotCount or > MountedMax
                    ? new Result(ResultKind.NoReply)
                    : new Result(ResultKind.Dismount, ctx.AnimalIndex - SlotCount);

            default:
                return new Result(ResultKind.Disconnect);
        }
    }

    public readonly record struct Result(ResultKind Kind, int NewAnimalIndex = 0, int NewAnimalNumber = 0);

    public readonly record struct Context(int AnimalIndex, int AnimalTime, int ActionSort, ImmutableArray<int> Garage);
}
