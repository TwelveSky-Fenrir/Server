using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses <c>005_00002.IMG</c> and replays <c>MyShm::Load_Item</c>'s per-load patches (S15_MyShare.cpp:422-514):
///     zeroed retired slots, the sell-lock range, elite-exchange-lock. Does NOT replay <c>SetInw33Item</c>
///     (already baked into the file) or <c>ChangeItemSort</c> remaps (itemsort99.h; display-only, not core stats).
/// </summary>
internal static class ItemReader
{
    private const string FileName = "005_00002.IMG";
    private const int XorKey = 0x1A80;
    private const int RecordArrayOffset = 40;
    private const int RecordCount = 99999;
    private const int RecordSize = 436;

    private const int Ielite = 4;
    private static readonly int[] ElitesExchangeLockSorts = [13, 14, 15, 9, 12, 10, 11, 7];

    /// <summary>Raw parse, no patches -- matches a raw <c>ts25ztool export item</c> CSV dump.</summary>
    public static IReadOnlyList<ItemRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var items = new List<ItemRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            items.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return items;
    }

    /// <summary>Patched records -- what item data looks like at runtime; seed this into SQL Server.</summary>
    public static IReadOnlyList<ItemRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory).Select(ApplyRuntimePatches).ToList();
    }

    private static ItemRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var name = reader.ReadFixedString(25);
        var description = new[] { reader.ReadFixedString(51), reader.ReadFixedString(51), reader.ReadFixedString(51) };
        reader.Skip(2); // compiler padding before iType (offset 182 -> 184)

        var type = reader.ReadInt32();
        var sort = reader.ReadInt32();
        var dataNumber2D = reader.ReadInt32();
        var dataNumber3D = reader.ReadInt32();
        var addDataNumber3D = reader.ReadInt32();
        var level = reader.ReadInt32();
        var martialLevel = reader.ReadInt32();
        var equipInfo = reader.ReadInt32Array(2);
        var buyCost = reader.ReadInt32();
        var sellCost = reader.ReadInt32();
        var buyCost2 = reader.ReadInt32();
        var levelLimit = reader.ReadInt32();
        var martialLevelLimit = reader.ReadInt32();
        var checkMonsterDrop = reader.ReadInt32();
        var checkNpcSell = reader.ReadInt32();
        var checkNpcShop = reader.ReadInt32();
        var checkAvatarDrop = reader.ReadInt32();
        var checkAvatarTrade = reader.ReadInt32();
        var checkAvatarShop = reader.ReadInt32();
        var checkImprove = reader.ReadInt32();
        var checkHighImprove = reader.ReadInt32();
        var checkHighItem = reader.ReadInt32();
        var checkLowItem = reader.ReadInt32();
        var checkExchange = reader.ReadInt32();
        var checkSetItem = reader.ReadInt32();
        var checkDateItem = reader.ReadInt32();
        var strength = reader.ReadInt32();
        var dexterity = reader.ReadInt32();
        var vitality = reader.ReadInt32();
        var intelligent = reader.ReadInt32();
        var luck = reader.ReadInt32();
        var attackPower = reader.ReadInt32();
        var defensePower = reader.ReadInt32();
        var attackSuccess = reader.ReadInt32();
        var attackBlock = reader.ReadInt32();
        var elementAttackPower = reader.ReadInt32();
        var elementDefensePower = reader.ReadInt32();
        var critical = reader.ReadInt32();
        var potionType = reader.ReadInt32Array(2);
        var gainSkillNumber = reader.ReadInt32();
        var lastAttackBonusInfo = reader.ReadInt32Array(2);
        var capeInfo = reader.ReadInt32Array(3);
        var bonusSkillInfo = new int[8][];
        for (var i = 0; i < 8; i++) bonusSkillInfo[i] = reader.ReadInt32Array(2);

        return new ItemRecord(
            index, name, description, type, sort, dataNumber2D, dataNumber3D, addDataNumber3D, level, martialLevel,
            equipInfo, buyCost, sellCost, buyCost2, levelLimit, martialLevelLimit, checkMonsterDrop, checkNpcSell,
            checkNpcShop, checkAvatarDrop, checkAvatarTrade, checkAvatarShop, checkImprove, checkHighImprove,
            checkHighItem, checkLowItem, checkExchange, checkSetItem, checkDateItem, strength, dexterity, vitality,
            intelligent, luck, attackPower, defensePower, attackSuccess, attackBlock, elementAttackPower,
            elementDefensePower, critical, potionType, gainSkillNumber, lastAttackBonusInfo, capeInfo, bonusSkillInfo);
    }

    private static ItemRecord ApplyRuntimePatches(ItemRecord item)
    {
        if (item.Index is >= 89501 and < 89563 or 99001)
            item = item with { Index = 0 };

        if (item.Index is >= 74200 and <= 74223)
            item = item with { SellCost = 1, CheckNpcSell = 2 };

        if (item.Type == Ielite && ElitesExchangeLockSorts.Contains(item.Sort))
            item = item with { CheckExchange = 2 };

        return item;
    }
}
