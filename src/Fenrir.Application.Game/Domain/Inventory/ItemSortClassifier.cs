namespace Fenrir.Application.Game.Domain.Inventory;

public static class ItemSortClassifier
{
    public const int NotFound = -1;

    public const int Unclassified = 0;

    private const int NewEliteGrade = 5;

    private const int GradeAboveNewElite = 6;

    public static int Classify(ItemRowDto item)
    {
        return Classify(item.Type, item.EquipInfo2, item.ItemId, item.Sort);
    }

    public static int Classify(int itemType, int equipPartTag, int itemId, int itemSort)
    {
        // Grade 5 never falls through to the itemId / sort-28 arms, it returns 0 instead
        // (Server/ts25zone/S07_MyGame03.cpp:6812-6837). equipPartTag is iEquipInfo[1].
        if (itemType == NewEliteGrade)
            return equipPartTag switch
            {
                2 or 4 or 5 or 6 or 7 or 9 => 1,
                11 or 12 or 13 or 14 => 2,
                _ => itemSort switch
                {
                    31 => 5,
                    32 => 6,
                    33 => 10,
                    8 => 8,
                    29 => 9,
                    _ => Unclassified
                }
            };

        if (itemType == GradeAboveNewElite)
            return 4;

        if (itemId is >= 201 and <= 218 or >= 2303 and <= 2305)
            return 3;

        return itemSort == 28 ? 7 : Unclassified;
    }
}
