namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Catalog ids, wire-selector constants, and wire result codes for CZ_PROCESS_DATA_SEND tSort 3000 --
///     rune-stone stat crafting on equipment. See <see cref="RuneStoneCraftResolver" /> for the rule engine
///     that consumes these.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:898-1127 (whole routine) ; :942-946 (source-item
///     whitelist) ; :947-951 (stat-slot-selector whitelist) ; :957-961 (destination "rune core" whitelist,
///     delegating to Server/Header/function.h:3388-3398, which defines the 4 legal ids) ;
///     Server/BuildEU33/ITEM_DUMP_CLEAN.csv:34236-34238 (source items' catalog names/effect text) ;
///     :34322-34325 (destination items' catalog names/description text).
/// </remarks>
public static class RuneStoneCraftCatalog
{
    /// <summary>92296 -- "add one random stat to the first empty slot", fixed STR/DEX/VIT/INT scan order.</summary>
    public const int AddStatItemId = 92296;

    /// <summary>92297 -- "reroll all four stats at once"; requires all four already filled.</summary>
    public const int RerollAllStatsItemId = 92297;

    /// <summary>92298 -- "reroll one selected stat", named by one of the 4 <c>StatSlotSelector*</c> constants.</summary>
    public const int RerollOneStatItemId = 92298;

    /// <summary>Wire "X position" value naming the STR sub-stat -- only meaningful for <see cref="RerollOneStatItemId" />.</summary>
    public const int StatSlotSelectorStrength = 100;

    public const int StatSlotSelectorDexterity = 200;
    public const int StatSlotSelectorVitality = 300;
    public const int StatSlotSelectorIntelligence = 400;

    private const int RuneCoreId1 = 93514;
    private const int RuneCoreId2 = 93515;
    private const int RuneCoreId3 = 93516;
    private const int RuneCoreId4 = 93517;

    /// <summary>The crafting log's own "-1" convention for the 2 branches that don't touch one specific slot.</summary>
    public const int NoSpecificSlot = -1;

    public const int ResultCodeSuccess = 0;
    public const int ResultCodeAllStatsAlreadyFilled = 10;
    public const int ResultCodeNotAllStatsFilled = 11;
    public const int ResultCodeSelectedStatSuccess = 12;

    /// <summary>
    ///     Dead in the shipped build: a second, redundant stat-slot-selector check inside the 92298 branch
    ///     that can never actually run in practice, because <see cref="RuneStoneCraftResolver.Resolve" />
    ///     already rejects an invalid selector earlier as a disconnect (S04_MyWork05.cpp:947-951). Kept here
    ///     only for parity-tracking/citation purposes -- <see cref="RuneStoneCraftResolver" /> never returns it.
    /// </summary>
    public const int ResultCodeInvalidSelectorDeadCode = 13;

    public const int ResultCodeSelectedStatEmpty = 14;

    public static bool IsSourceItem(int itemId)
    {
        return itemId is AddStatItemId or RerollAllStatsItemId or RerollOneStatItemId;
    }

    public static bool IsDestinationItem(int itemId)
    {
        return itemId is RuneCoreId1 or RuneCoreId2 or RuneCoreId3 or RuneCoreId4;
    }

    public static bool IsValidStatSlotSelector(int selector)
    {
        return selector is StatSlotSelectorStrength or StatSlotSelectorDexterity or StatSlotSelectorVitality
            or StatSlotSelectorIntelligence;
    }

    /// <summary>
    ///     The legacy crafting log's own literal action labels (Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:549-568),
    ///     one per source item. Only ever called once <see cref="IsSourceItem" /> is already known true for
    ///     <paramref name="sourceItemId" />.
    /// </summary>
    public static string GetLogActionLabel(int sourceItemId)
    {
        return sourceItemId switch
        {
            AddStatItemId => "STATADD",
            RerollAllStatsItemId => "RANDOMSTAT",
            RerollOneStatItemId => "SLOTSTAT",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceItemId), sourceItemId,
                "Not one of the 3 rune-stone-crafting source items -- caller must check IsSourceItem first.")
        };
    }
}
