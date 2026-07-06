using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses <c>005_00002.IMG</c> and replays <c>MyShm::Load_Item</c>'s per-load patches
///     (S15_MyShare.cpp:422-514, 1192-1269): zeroed retired slots, the 2021.04.10 "no drop"
///     <c>CheckMonsterDrop</c> catalog fix, and the elite-exchange-lock. Does NOT replay <c>SetInw33Item</c>
///     (already baked into the file), <c>ChangeItemSort</c> remaps (itemsort99.h; display-only, not core
///     stats), or the <c>USE_CUSTOME_CREATE</c> sell-lock override for items 74200-74223
///     (S15_MyShare.cpp:463-469) -- that macro is defined only in the non-<c>M33</c> <c>#else</c> branch
///     (Header/Protocol/DEFINE.h:21-51) and both real build configurations, <c>ReleaseM33</c> and
///     <c>ReleaseEU33</c>, define <c>M33</c> unconditionally (ts25latest_config.props:4-12), so it never
///     compiles into any shipped binary.
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

    /// <summary>
    ///     2021.04.10 "no drop" fix (S15_MyShare.cpp:1192-1253): these identifiers have <c>CheckMonsterDrop</c>
    ///     forced to 1, unconditionally. Identifiers 1072-1074 are deliberately excluded here -- they sit
    ///     inside the same source switch-statement but gated by <c>#ifdef NO_DROP_MONEY_BAR</c>, which is never
    ///     <c>#define</c>'d anywhere under <c>Server/</c>, so that sub-case is dead code in every real build.
    ///     Also includes the immediately-adjacent <c>#elif defined MG5ORIGIN</c> extension
    ///     (S15_MyShare.cpp:1259-1267): identifiers 7001-7027 and 17001-17133. <c>MG5ORIGIN</c> is defined
    ///     unconditionally outside any build-configuration branch (Header/Protocol/DEFINE.h:18), so it is
    ///     active in every real build including ReleaseEU33 -- unlike the sibling <c>#ifdef PWSEA</c> branch
    ///     (identifiers 7000-7425), which is commented out at its definition site and therefore excluded here
    ///     as dead code.
    /// </summary>
    private static bool IsNoDropForcedOne(int index)
    {
        return index is 706 or 708 or 709 or 710 or 711
            or >= 865 and <= 885
            or 983
            or >= 1079 and <= 1091
            or 1125 or 1369 or 1989
            or >= 2001 and <= 2004
            or >= 7001 and <= 7027
            or >= 17001 and <= 17133;
    }

    /// <summary>
    ///     2021.04.10 "no drop" fix (S15_MyShare.cpp:1249-1252): these identifiers have <c>CheckMonsterDrop</c>
    ///     forced to 2, unconditionally.
    /// </summary>
    private static bool IsNoDropForcedTwo(int index)
    {
        return index is 611 or 612;
    }

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

        if (IsNoDropForcedTwo(item.Index))
            item = item with { CheckMonsterDrop = 2 };
        else if (IsNoDropForcedOne(item.Index))
            item = item with { CheckMonsterDrop = 1 };

        if (item.Type == Ielite && ElitesExchangeLockSorts.Contains(item.Sort))
            item = item with { CheckExchange = 2 };

        return item;
    }
}
