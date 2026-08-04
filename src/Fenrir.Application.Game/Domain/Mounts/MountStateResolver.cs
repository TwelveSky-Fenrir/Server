using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountStateResolver
{
    public enum ResultKind
    {
        NoReply,

        Disconnect,
        Select,
        Deselect,
        Mount,
        Dismount,

        DeleteMount,

        DeleteAttribute,

        Convert,

        Transfer
    }

    public const int SlotCount = 10;

    public const int MountedMax = 19;

    public const int MaxMountExp = 100_000;

    public const int MaxRolledAttributeTotal = 25;

    public const int StatSlotCount = 8;

    public static bool IsOrdinarySlot(int animalIndex)
    {
        return animalIndex is >= 0 and < SlotCount;
    }

    public static bool IsMountedSlot(int animalIndex)
    {
        return animalIndex is >= SlotCount and <= MountedMax;
    }

    public static bool TryGetOwnedMount(ImmutableArray<int> garage, int garageSlot, out int itemId)
    {
        if (garageSlot is >= 0 and < SlotCount && !garage.IsDefault && garageSlot < garage.Length &&
            garage[garageSlot] > 0)
        {
            itemId = garage[garageSlot];
            return true;
        }

        itemId = 0;
        return false;
    }

    public static bool TryResolveActiveMountedMount(int animalIndex, int animalNumber, ImmutableArray<int> garage,
        out int garageSlot)
    {
        garageSlot = animalIndex - SlotCount;
        return IsMountedSlot(animalIndex) && animalNumber > 0 &&
               TryGetOwnedMount(garage, garageSlot, out var rosterItemId) && rosterItemId == animalNumber;
    }

    public static Result Resolve(int sort, int value, in Context ctx)
    {
        switch (sort)
        {
            case 1:
                return !TryGetOwnedMount(ctx.Garage, value, out _)
                    ? new Result(ResultKind.NoReply)
                    : new Result(ResultKind.Select, value);

            case 2:
                if (!IsOrdinarySlot(value) || !IsOrdinarySlot(ctx.AnimalIndex))
                    return new Result(ResultKind.NoReply);
                return new Result(ResultKind.Deselect, -1);

            case 3:
                if (!IsOrdinarySlot(ctx.AnimalIndex) || ctx.AnimalTime < 1 || ctx.ActionSort != 1 ||
                    !TryGetOwnedMount(ctx.Garage, ctx.AnimalIndex, out var animalNumber))
                    return new Result(ResultKind.NoReply);

                var mountedIndex = ctx.AnimalIndex + SlotCount;
                return new Result(ResultKind.Mount, mountedIndex, animalNumber);

            case 4:
                return !IsMountedSlot(ctx.AnimalIndex)
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

    public static int AttributeIndex(int garageSlot, int statSlotIndex)
    {
        return garageSlot * StatSlotCount + statSlotIndex;
    }

    private static int GarageSlotOf(int animalIndex)
    {
        return animalIndex >= SlotCount ? animalIndex - SlotCount : animalIndex;
    }

    private static Result ResolveDeleteMount(int value, in Context ctx)
    {
        if (!IsOrdinarySlot(ctx.AnimalIndex))
            return new Result(ResultKind.Disconnect);

        if (value == 0)
            return new Result(ResultKind.Disconnect);

        for (var i = 0; i < SlotCount; i++)
            if (TryGetOwnedMount(ctx.Garage, i, out var itemId) && itemId == value)
                return new Result(ResultKind.DeleteMount, GarageSlot: i);

        return new Result(ResultKind.Disconnect);
    }

    private static Result ResolveConvertAttribute(in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.Disconnect);

        var slot = GarageSlotOf(ctx.AnimalIndex);

        if (!TryGetOwnedMount(ctx.Garage, slot, out _))
            return new Result(ResultKind.Disconnect);

        if (ValueAt(ctx.AccumulatedExp, slot) != MaxMountExp)
            return new Result(ResultKind.Disconnect);

        if (ValueAt(ctx.RolledAttributeTotal, slot) >= MaxRolledAttributeTotal)
            return new Result(ResultKind.Disconnect);

        return new Result(ResultKind.Convert, GarageSlot: slot);
    }

    private static Result ResolveDeleteAttribute(int value, in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.Disconnect);

        if (value is < 1 or > StatSlotCount)
            return new Result(ResultKind.Disconnect);

        if (!ctx.HasAttributeDeleteMaterial)
            return new Result(ResultKind.Disconnect);

        var slot = GarageSlotOf(ctx.AnimalIndex);
        if (!TryGetOwnedMount(ctx.Garage, slot, out _))
            return new Result(ResultKind.Disconnect);
        return new Result(ResultKind.DeleteAttribute, GarageSlot: slot, StatSlotIndex: value - 1);
    }

    private static Result ResolveTransferAttribute(int value, in Context ctx)
    {
        if (ctx.AnimalIndex < 0 || ctx.AnimalIndex > MountedMax)
            return new Result(ResultKind.NoReply);

        if (value is < 1 or > StatSlotCount)
            return new Result(ResultKind.Disconnect);

        if (!ctx.HasAttributeTransferMaterial)
            return new Result(ResultKind.Disconnect);

        var slot = GarageSlotOf(ctx.AnimalIndex);
        if (!TryGetOwnedMount(ctx.Garage, slot, out _))
            return new Result(ResultKind.Disconnect);
        return new Result(ResultKind.Transfer, GarageSlot: slot, StatSlotIndex: value - 1);
    }

    private static int ValueAt(ImmutableArray<int> values, int index)
    {
        return !values.IsDefault && index >= 0 && index < values.Length ? values[index] : 0;
    }

    public readonly record struct Result(
        ResultKind Kind,
        int NewAnimalIndex = 0,
        int NewAnimalNumber = 0,
        int GarageSlot = -1,
        int StatSlotIndex = -1);

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
