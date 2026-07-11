using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Crafting;

public enum RuneStoneCraftOutcome
{

        Disconnect,

        Refused,

        Applied
}

public readonly record struct RuneStoneCraftRequest(
    int SourcePage,
    int SourceSlot,
    int SourceItemId,
    int DestinationPage,
    int DestinationSlot,
    int DestinationItemId,
    int DestinationPackedStat,
    int StatSlotSelector,
    bool SecondInventoryPageAccessible);

public readonly record struct RuneStoneCraftResult(
    RuneStoneCraftOutcome Outcome,
    int ResultCode,
    int NewPackedStat,
    int LogSlotIndicator)
{
    public static readonly RuneStoneCraftResult Disconnect =
        new(RuneStoneCraftOutcome.Disconnect, 0, 0, RuneStoneCraftCatalog.NoSpecificSlot);

    public bool Succeeded => Outcome == RuneStoneCraftOutcome.Applied;
}

public static class RuneStoneCraftResolver
{
    public static RuneStoneCraftResult Resolve(RuneStoneCraftRequest request, IRandomSource random)
    {
        if (!IsValidInventorySlot(request.SourcePage, request.SourceSlot) ||
            !IsValidInventorySlot(request.DestinationPage, request.DestinationSlot))
            return RuneStoneCraftResult.Disconnect;

        if ((request.SourcePage == ContainerMatrix.InventoryPage1 ||
             request.DestinationPage == ContainerMatrix.InventoryPage1) &&
            !request.SecondInventoryPageAccessible)
            return RuneStoneCraftResult.Disconnect;

        if (!RuneStoneCraftCatalog.IsSourceItem(request.SourceItemId))
            return RuneStoneCraftResult.Disconnect;

        if (request.SourceItemId == RuneStoneCraftCatalog.RerollOneStatItemId &&
            !RuneStoneCraftCatalog.IsValidStatSlotSelector(request.StatSlotSelector))
            return RuneStoneCraftResult.Disconnect;

        if (!RuneStoneCraftCatalog.IsDestinationItem(request.DestinationItemId))
            return RuneStoneCraftResult.Disconnect;

        var strRoll = (sbyte)RuneStoneStatRollTable.Roll(random);
        var dexRoll = (sbyte)RuneStoneStatRollTable.Roll(random);
        var vitRoll = (sbyte)RuneStoneStatRollTable.Roll(random);
        var intRoll = (sbyte)RuneStoneStatRollTable.Roll(random);

        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(request.DestinationPackedStat);

        return request.SourceItemId switch
        {
            RuneStoneCraftCatalog.AddStatItemId =>
                ResolveAddStat(str, dex, vit, intel, strRoll, dexRoll, vitRoll, intRoll),
            RuneStoneCraftCatalog.RerollAllStatsItemId =>
                ResolveRerollAll(str, dex, vit, intel, strRoll, dexRoll, vitRoll, intRoll),
            _ => ResolveRerollOne(request.StatSlotSelector, str, dex, vit, intel, strRoll, dexRoll, vitRoll, intRoll)
        };
    }

        public static ItemStack? ConsumeOneUnit(ItemStack source)
    {
        var remaining = source.Quantity - 1;
        return remaining >= 1 ? source with { Quantity = remaining } : null;
    }

    private static bool IsValidInventorySlot(int page, int slot)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, slot);
    }

    private static RuneStoneCraftResult ResolveAddStat(sbyte str, sbyte dex, sbyte vit, sbyte intel,
        sbyte strRoll, sbyte dexRoll, sbyte vitRoll, sbyte intRoll)
    {
        if (str <= 0)
            return Applied(RuneStoneStatCodec.Encode(strRoll, dex, vit, intel));
        if (dex <= 0)
            return Applied(RuneStoneStatCodec.Encode(str, dexRoll, vit, intel));
        if (vit <= 0)
            return Applied(RuneStoneStatCodec.Encode(str, dex, vitRoll, intel));
        if (intel <= 0)
            return Applied(RuneStoneStatCodec.Encode(str, dex, vit, intRoll));

        return Refused(RuneStoneCraftCatalog.ResultCodeAllStatsAlreadyFilled);
    }

    private static RuneStoneCraftResult ResolveRerollAll(sbyte str, sbyte dex, sbyte vit, sbyte intel,
        sbyte strRoll, sbyte dexRoll, sbyte vitRoll, sbyte intRoll)
    {
        if (str <= 0 || dex <= 0 || vit <= 0 || intel <= 0)
            return Refused(RuneStoneCraftCatalog.ResultCodeNotAllStatsFilled);

        return Applied(RuneStoneStatCodec.Encode(strRoll, dexRoll, vitRoll, intRoll));
    }

        private static RuneStoneCraftResult ResolveRerollOne(int statSlotSelector, sbyte str, sbyte dex, sbyte vit,
        sbyte intel, sbyte strRoll, sbyte dexRoll, sbyte vitRoll, sbyte intRoll)
    {
        return statSlotSelector switch
        {
            RuneStoneCraftCatalog.StatSlotSelectorStrength => str == 0
                ? RefusedSlot(1)
                : AppliedSlot(RuneStoneStatCodec.Encode(strRoll, dex, vit, intel), 1),
            RuneStoneCraftCatalog.StatSlotSelectorDexterity => dex == 0
                ? RefusedSlot(2)
                : AppliedSlot(RuneStoneStatCodec.Encode(str, dexRoll, vit, intel), 2),
            RuneStoneCraftCatalog.StatSlotSelectorVitality => vit == 0
                ? RefusedSlot(3)
                : AppliedSlot(RuneStoneStatCodec.Encode(str, dex, vitRoll, intel), 3),
            _ => intel == 0
                ? RefusedSlot(4)
                : AppliedSlot(RuneStoneStatCodec.Encode(str, dex, vit, intRoll), 4)
        };
    }

    private static RuneStoneCraftResult Applied(int newPackedStat)
    {
        return new RuneStoneCraftResult(RuneStoneCraftOutcome.Applied, RuneStoneCraftCatalog.ResultCodeSuccess,
            newPackedStat, RuneStoneCraftCatalog.NoSpecificSlot);
    }

    private static RuneStoneCraftResult AppliedSlot(int newPackedStat, int slotIndicator)
    {
        return new RuneStoneCraftResult(RuneStoneCraftOutcome.Applied,
            RuneStoneCraftCatalog.ResultCodeSelectedStatSuccess, newPackedStat, slotIndicator);
    }

    private static RuneStoneCraftResult Refused(int resultCode)
    {
        return new RuneStoneCraftResult(RuneStoneCraftOutcome.Refused, resultCode, 0,
            RuneStoneCraftCatalog.NoSpecificSlot);
    }

    private static RuneStoneCraftResult RefusedSlot(int slotIndicator)
    {
        return new RuneStoneCraftResult(RuneStoneCraftOutcome.Refused,
            RuneStoneCraftCatalog.ResultCodeSelectedStatEmpty,
            0, slotIndicator);
    }
}
