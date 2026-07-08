using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     Pure resolver for CZ_ANIMAL_STATE_SEND (op87)'s eight live sub-actions (Sort 1-8). No I/O, no Zone
///     dependency.
/// </summary>
/// <remarks>
///     Sort 1-4 (Select/Deselect/Mount/Dismount) and Sort 5 (Delete Mount)/7 (Delete Rolled Attribute) are
///     fully implemented, including their success paths. Sort 6 (Convert Attribute) and Sort 8 (Transfer
///     Rolled Attribute) implement every documented bounds/gating check, but their own "roll"/"transfer"
///     mechanics have no cataloged probability table or formula (no legacy-behavior-translator contract
///     supplies one) -- per this codebase's hard rule against reproducing legacy numeric behavior from
///     memory, both are modeled as always taking their documented FAILURE branch (a plain disconnect for
///     Sort 6; a "zero-result transfer" revert-then-disconnect for Sort 8) until a follow-up contract
///     supplies the real mechanic. This is provably a no-op today regardless of the placeholder: Sort 6's own
///     upstream gate (<see cref="PlayerRuntimeState" />'s <c>MountAccumulatedExp</c> must exactly equal
///     <see cref="MaxMountExp" />) can never be satisfied since no mount-experience-gain system exists yet
///     (same "real but currently unreachable" posture as <c>AnimalTime</c>/<c>AnimalAbsorbTine</c>), and Sort
///     8's rolled-attribute values can only ever be non-zero via Sort 6's own (currently unreachable) success
///     path, so there is no value a transfer could move even if its formula were modeled.
///     <para>
///         Sort 5 (Delete Mount) is additionally hardened against a verified legacy quirk: the C++ source
///         searches <c>aAnimal[10]</c> for a slot whose VALUE equals the request's <c>tValue</c> with a raw
///         equality compare, so requesting deletion of "mount id 0" would match the first EMPTY slot and
///         grant +250 CP for free, with no real mount ever required. This resolver requires the matched slot
///         to hold a non-zero item id, closing that free-currency surface per this codebase's standing hard
///         security constraint against any currency/XP gain through a non-GM detour path -- same posture as
///         <c>TribeActionService.RebirthAsync</c>'s own hardening against a different legacy data-loss quirk.
///         See <c>EventLogCategory.MountAttribute</c>'s own remarks for this project's prior audit of the
///         same quirk (previously closed by leaving Sort 5+ entirely unimplemented; now closed by this
///         narrower, targeted guard instead).
///     </para>
///     Sort 120 (tier "rank-up") is compiled only under a non-production build variant (<c>#ifndef LNW33</c>)
///     and must never be reachable in a Fenrir port targeting the EU33/LNW33 production build -- it falls to
///     the same <see cref="ResultKind.Disconnect" /> default as any other unrecognized sort.
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
        Dismount,

        /// <summary>Sort 5 success -- see <see cref="Result.GarageSlot" />.</summary>
        DeleteMount,

        /// <summary>Sort 7 success -- see <see cref="Result.GarageSlot" />/<see cref="Result.StatSlotIndex" />.</summary>
        DeleteAttribute
    }

    /// <summary>MAX_AVATAR_ANIMAL_NUM.</summary>
    public const int SlotCount = 10;

    /// <summary>MAX_AVATAR_ANIMAL_NUM2 -- the highest AnimalIndex value considered "mounted."</summary>
    public const int MountedMax = 19;

    /// <summary>MAX_MOUNT_EXP -- Sort 6's exact-match accumulated-experience gate.</summary>
    public const int MaxMountExp = 100_000;

    /// <summary>Sort 6's rolled-attribute-total cap (strictly below this value to proceed).</summary>
    public const int MaxRolledAttributeTotal = 25;

    /// <summary>Legal wire range for the stat-slot companion value Sort 7/8 address (1-8, stored 0-based).</summary>
    public const int StatSlotCount = 8;

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

            case 5:
                return ResolveDeleteMount(value, in ctx);

            case 6:
                return ResolveConvertAttribute(in ctx);

            case 7:
                return ResolveDeleteAttribute(value, in ctx);

            case 8:
                return ResolveTransferAttribute(value, in ctx);

            default:
                return new Result(ResultKind.Disconnect);
        }
    }

    /// <summary>Flattens a (garage slot, 0-based stat slot) pair into <see cref="PlayerRuntimeState.MountRolledAttributes" />'s index space.</summary>
    public static int AttributeIndex(int garageSlot, int statSlotIndex)
    {
        return garageSlot * StatSlotCount + statSlotIndex;
    }

    /// <summary>aAnimalIndex's dual 0-9/10-19 encoding collapsed onto its underlying garage slot.</summary>
    private static int GarageSlotOf(int animalIndex)
    {
        return animalIndex >= SlotCount ? animalIndex - SlotCount : animalIndex;
    }

    /// <summary>
    ///     Server/ts25zone/S04_MyWork02.cpp:11890-11919 -- requires the selection index in the unmounted
    ///     range AND the companion value to match a non-zero item id actually stored in one of the owned-mount
    ///     slots (see this type's own &lt;remarks&gt; for why "non-zero" is a deliberate hardening, not a
    ///     literal transliteration).
    /// </summary>
    private static Result ResolveDeleteMount(int value, in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex >= SlotCount)
            return new Result(ResultKind.Disconnect);

        if (value == 0)
            return new Result(ResultKind.Disconnect);

        for (var i = 0; i < SlotCount; i++)
            if (ctx.Garage[i] == value)
                return new Result(ResultKind.DeleteMount, GarageSlot: i);

        return new Result(ResultKind.Disconnect);
    }

    /// <summary>
    ///     Server/ts25zone/S04_MyWork02.cpp:11920-11946 -- full bounds/gating chain; the roll itself is a
    ///     documented placeholder, see this type's own &lt;remarks&gt;.
    /// </summary>
    private static Result ResolveConvertAttribute(in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.Disconnect);

        var slot = GarageSlotOf(ctx.AnimalIndex);

        if (ctx.AccumulatedExp[slot] != MaxMountExp)
            return new Result(ResultKind.Disconnect);

        if (ctx.RolledAttributeTotal[slot] >= MaxRolledAttributeTotal)
            return new Result(ResultKind.Disconnect);

        // Attribute-conversion roll: no cataloged probability table exists for this range yet -- always
        // takes the documented "roll produced zero" failure branch. See this type's own <remarks>.
        return new Result(ResultKind.Disconnect);
    }

    /// <summary>Server/ts25zone/S04_MyWork02.cpp:11959-11991 -- full bounds/gating chain, deterministic success.</summary>
    private static Result ResolveDeleteAttribute(int value, in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.Disconnect);

        if (value is < 1 or > StatSlotCount)
            return new Result(ResultKind.Disconnect);

        if (!ctx.HasAttributeDeleteMaterial)
            return new Result(ResultKind.Disconnect);

        var slot = GarageSlotOf(ctx.AnimalIndex);
        return new Result(ResultKind.DeleteAttribute, GarageSlot: slot, StatSlotIndex: value - 1);
    }

    /// <summary>
    ///     Server/ts25zone/S04_MyWork02.cpp:11993-12044 -- the one live sub-action whose PRIMARY index check
    ///     is a silent no-op rather than a disconnect. The transfer mechanic itself is a documented
    ///     placeholder, see this type's own &lt;remarks&gt;.
    /// </summary>
    private static Result ResolveTransferAttribute(int value, in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.NoReply);

        if (value is < 1 or > StatSlotCount)
            return new Result(ResultKind.Disconnect);

        if (!ctx.HasAttributeTransferMaterial)
            return new Result(ResultKind.Disconnect);

        // Transfer-operation roll: no cataloged formula exists for this range yet. The ONE branch this
        // contract fully specifies -- a zero/failure result reverts the provisional value then disconnects
        // -- is exactly what always executing here reproduces; no provisional value is ever written in the
        // first place (nothing is read from PlayerRuntimeState.MountRolledAttributes above), so "revert" is
        // a no-op. See this type's own <remarks>.
        return new Result(ResultKind.Disconnect);
    }

    /// <summary>
    ///     <paramref name="GarageSlot" />/<paramref name="StatSlotIndex" /> are only meaningful for
    ///     <see cref="ResultKind.DeleteMount" />/<see cref="ResultKind.DeleteAttribute" /> respectively (-1
    ///     otherwise).
    /// </summary>
    public readonly record struct Result(
        ResultKind Kind,
        int NewAnimalIndex = 0,
        int NewAnimalNumber = 0,
        int GarageSlot = -1,
        int StatSlotIndex = -1);

    /// <param name="AccumulatedExp">Sort 6 only -- <see cref="PlayerRuntimeState.MountAccumulatedExp" />.</param>
    /// <param name="RolledAttributeTotal">Sort 6 only -- <see cref="PlayerRuntimeState.MountRolledAttributeTotal" />.</param>
    /// <param name="HasAttributeDeleteMaterial">Sort 7 only -- item 1225 present anywhere in inventory.</param>
    /// <param name="HasAttributeTransferMaterial">Sort 8 only -- item 8425 or 1226 present anywhere in inventory.</param>
    public readonly record struct Context(
        int AnimalIndex,
        int AnimalTime,
        int ActionSort,
        ImmutableArray<int> Garage,
        ImmutableArray<int> AccumulatedExp,
        ImmutableArray<int> RolledAttributeTotal,
        bool HasAttributeDeleteMaterial,
        bool HasAttributeTransferMaterial);
}
