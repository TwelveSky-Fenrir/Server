namespace Fenrir.Application.Game.Domain.Crafting;

public static class RuneStoneCraftCatalog
{
    public const int AddStatItemId = 92296;

    public const int RerollAllStatsItemId = 92297;

    public const int RerollOneStatItemId = 92298;

    public const int StatSlotSelectorStrength = 100;

    public const int StatSlotSelectorDexterity = 200;
    public const int StatSlotSelectorVitality = 300;
    public const int StatSlotSelectorIntelligence = 400;

    private const int RuneCoreId1 = 93514;
    private const int RuneCoreId2 = 93515;
    private const int RuneCoreId3 = 93516;
    private const int RuneCoreId4 = 93517;

    public const int NoSpecificSlot = -1;

    public const int ResultCodeSuccess = 0;
    public const int ResultCodeAllStatsAlreadyFilled = 10;
    public const int ResultCodeNotAllStatsFilled = 11;
    public const int ResultCodeSelectedStatSuccess = 12;

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
