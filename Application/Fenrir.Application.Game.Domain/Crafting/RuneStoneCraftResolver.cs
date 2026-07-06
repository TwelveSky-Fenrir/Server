using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>Coarse outward posture for a resolved rune-stone-craft request.</summary>
public enum RuneStoneCraftOutcome
{
    /// <summary>Malformed input -- the caller must disconnect the session for this request; no reply is sent.</summary>
    Disconnect,

    /// <summary>A well-formed request the business rules cleanly refused -- the caller replies with <see cref="RuneStoneCraftResult.ResultCode" /> and a packed-value field of 0.</summary>
    Refused,

    /// <summary>The craft was applied -- the caller replies with <see cref="RuneStoneCraftResult.ResultCode" /> and <see cref="RuneStoneCraftResult.NewPackedStat" />.</summary>
    Applied
}

/// <summary>
///     One CZ_PROCESS_DATA_SEND tSort-3000 request, already reduced to the plain values
///     <see cref="RuneStoneCraftResolver" /> needs -- item ids (0 if the corresponding slot was empty) instead
///     of raw page/slot ItemStack lookups, which is the caller's job (mirrors
///     <c>SaveBankItemTransferPolicy</c>'s own "caller resolves ItemStack, policy only sees ids/values" split).
/// </summary>
/// <param name="SourcePage">0 or 1 -- which general inventory page holds the source rune-stone item.</param>
/// <param name="SourceSlot">0-63 -- the source item's slot within <see cref="SourcePage" />.</param>
/// <param name="SourceItemId">The source slot's resolved catalog item id, or 0 if that slot is empty.</param>
/// <param name="DestinationPage">0 or 1 -- which general inventory page holds the destination rune-core item.</param>
/// <param name="DestinationSlot">0-63 -- the destination item's slot within <see cref="DestinationPage" />.</param>
/// <param name="DestinationItemId">The destination slot's resolved catalog item id, or 0 if that slot is empty.</param>
/// <param name="DestinationPackedStat">The destination item's current packed STR/DEX/VIT/INT value (see <see cref="RuneStoneStatCodec" />).</param>
/// <param name="StatSlotSelector">The wire's raw "X position" field -- only meaningful when <see cref="SourceItemId" /> is <see cref="RuneStoneCraftCatalog.RerollOneStatItemId" />.</param>
/// <param name="SecondInventoryPageAccessible">
///     Whether the character's second inventory page access has not expired. Caller-supplied because Fenrir
///     has no <c>wInventoryDate</c>-equivalent field anywhere yet -- same pre-existing gap
///     <c>SaveBankItemTransferPolicy</c>/<c>StoreItemTransferPolicy</c>/<c>PetBagItemTransferPolicy</c> already
///     document for their own second-page uses, not invented here.
/// </param>
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

/// <param name="Outcome">Which of the 3 outward postures this is -- see <see cref="RuneStoneCraftOutcome" />.</param>
/// <param name="ResultCode">
///     The literal wire result code (0/10/11/12/14) -- meaningless when <see cref="Outcome" /> is
///     <see cref="RuneStoneCraftOutcome.Disconnect" />, since no reply is ever sent on that path.
/// </param>
/// <param name="NewPackedStat">
///     The destination item's fresh packed STR/DEX/VIT/INT value on a genuine <see cref="RuneStoneCraftOutcome.Applied" />
///     success; 0 on every other outcome (the legacy leaves the wire's "new packed value" field untouched on
///     every refusal/disconnect path -- 0 is its zero-initialized default, not a real value).
/// </param>
/// <param name="LogSlotIndicator">
///     -1 ("no specific slot") for the 92296/92297 branches, or the 1-based stat position (1=STR/2=DEX/3=VIT/4=INT)
///     for the 92298 branch. Meaningless when nothing was actually logged (every non-<see cref="RuneStoneCraftOutcome.Applied" />
///     outcome).
/// </param>
public readonly record struct RuneStoneCraftResult(
    RuneStoneCraftOutcome Outcome,
    int ResultCode,
    int NewPackedStat,
    int LogSlotIndicator)
{
    public bool Succeeded => Outcome == RuneStoneCraftOutcome.Applied;

    public static readonly RuneStoneCraftResult Disconnect =
        new(RuneStoneCraftOutcome.Disconnect, 0, 0, RuneStoneCraftCatalog.NoSpecificSlot);
}

/// <summary>
///     Pure resolver for CZ_PROCESS_DATA_SEND tSort 3000 -- rune-stone stat crafting on equipment. No I/O, no
///     Zone/PlayerRuntimeState dependency: the caller resolves ItemStacks into a <see cref="RuneStoneCraftRequest" />
///     and gets back exactly one of the 3 outward postures <see cref="RuneStoneCraftOutcome" /> describes.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:898-1127 (whole routine, all branches, all result codes) ;
///     :904-915 (bounds) ; :917-925 (extended-page gate) ; :927-946 (catalog resolution + source whitelist) ;
///     :947-951 (stat-slot-selector whitelist, checked once up front -- see
///     <see cref="RuneStoneCraftCatalog.ResultCodeInvalidSelectorDeadCode" /> for why a 2nd, later check
///     inside the same branch is dead code, never reachable) ; :952-956 (inert, commented-out catalog-sort
///     check -- not reproduced here, matching the shipped build) ; :957-961 (destination whitelist) ;
///     :963-988 (roll generation -- see <see cref="RuneStoneStatRollTable" />) ; :992-1102 (the 3 branches and
///     their refusal codes) ; :1104-1127 (shared success tail for the 92296/92297 branches).
///     <para>
///         The 92298 branch's emptiness test is strictly "== 0" (S04_MyWork05.cpp:1040-1102), unlike
///         92296/92297's "&lt;= 0" test for the same concept -- a genuine legacy inconsistency between the 3
///         branches, ported as-is per the backing contract. Not a bug to silently normalize away.
///     </para>
/// </remarks>
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

        // 4 independent rolls, always drawn up front regardless of which branch runs or how many are applied.
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

    /// <summary>
    ///     Decrements the source stack by exactly 1 (never the wire's requested quantity, which this whole
    ///     behavior never reads) -- returns null once the remaining quantity drops below 1, meaning "clear
    ///     this slot" (the item and any socketed gem together, same posture as every other slot-clear in this
    ///     family). Only meaningful to call once <see cref="RuneStoneCraftResult.Succeeded" /> is true.
    /// </summary>
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

    /// <summary>
    ///     Emptiness here is strictly "== 0", not "&lt;= 0" like the other 2 branches -- see this type's own
    ///     remarks for why that asymmetry is intentionally preserved rather than normalized away.
    /// </summary>
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
        return new RuneStoneCraftResult(RuneStoneCraftOutcome.Refused, RuneStoneCraftCatalog.ResultCodeSelectedStatEmpty,
            0, slotIndicator);
    }
}
