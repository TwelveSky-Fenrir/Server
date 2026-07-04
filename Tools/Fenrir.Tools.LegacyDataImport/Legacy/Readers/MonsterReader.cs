using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>Parses <c>005_00004.IMG</c> (<c>MONSTER_INFO</c>, STRUCT.h:156-204); no known runtime patches, unlike <see cref="ItemReader" />.</summary>
internal static class MonsterReader
{
    private const string FileName = "005_00004.IMG";
    private const int XorKey = 0xC71;
    private const int RecordArrayOffset = 64;
    private const int RecordCount = 10000;
    private const int RecordSize = 940;

    /// <summary>Raw parse, no patches -- matches a raw <c>ts25ztool export monster</c> CSV dump.</summary>
    public static IReadOnlyList<MonsterRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var monsters = new List<MonsterRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            monsters.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return monsters;
    }

    public static IReadOnlyList<MonsterRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static MonsterRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var name = reader.ReadFixedString(25);
        var chatInfo = new[] { reader.ReadFixedString(101), reader.ReadFixedString(101) };
        reader.Skip(1); // compiler padding before mType (offset 231 -> 232)

        var type = reader.ReadInt32();
        var specialType = reader.ReadInt32();
        var damageType = reader.ReadInt32();
        var dataSortNumber = reader.ReadInt32();
        var size = reader.ReadInt32Array(4);
        var sizeCategory = reader.ReadInt32();
        var checkCollision = reader.ReadInt32();
        var frameInfo = reader.ReadInt32Array(6);
        var totalHitNum = reader.ReadInt32();
        var hitFrame = reader.ReadInt32Array(3);
        var totalSkillHitNum = reader.ReadInt32();
        var skillHitFrame = reader.ReadInt32Array(3);
        var bulletInfo = reader.ReadInt32Array(2);
        var summonTime = reader.ReadInt32Array(2);
        var itemLevel = reader.ReadInt32();
        var martialItemLevel = reader.ReadInt32();
        var realLevel = reader.ReadInt32();
        var martialRealLevel = reader.ReadInt32();
        var generalExperience = reader.ReadInt32();
        var patExperience = reader.ReadInt32();
        var life = reader.ReadInt32();
        var attackType = reader.ReadInt32();
        var radiusInfo = reader.ReadInt32Array(2);
        var walkSpeed = reader.ReadInt32();
        var runSpeed = reader.ReadInt32();
        var deathSpeed = reader.ReadInt32();
        var attackPower = reader.ReadInt32();
        var defensePower = reader.ReadInt32();
        var attackSuccess = reader.ReadInt32();
        var attackBlock = reader.ReadInt32();
        var elementAttackPower = reader.ReadInt32();
        var elementDefensePower = reader.ReadInt32();
        var critical = reader.ReadInt32();
        var followInfo = reader.ReadInt32Array(2);
        var dropMoneyInfo = reader.ReadInt32Array(3);
        var dropPotionInfo = new int[5][];
        for (var i = 0; i < 5; i++) dropPotionInfo[i] = reader.ReadInt32Array(2);
        var dropItemInfo = reader.ReadInt32Array(12);
        var dropQuestItemInfo = reader.ReadInt32Array(2);
        var dropExtraItemInfo = new int[50][];
        for (var i = 0; i < 50; i++) dropExtraItemInfo[i] = reader.ReadInt32Array(2);

        return new MonsterRecord(
            index, name, chatInfo, type, specialType, damageType, dataSortNumber, size, sizeCategory, checkCollision,
            frameInfo, totalHitNum, hitFrame, totalSkillHitNum, skillHitFrame, bulletInfo, summonTime, itemLevel,
            martialItemLevel, realLevel, martialRealLevel, generalExperience, patExperience, life, attackType,
            radiusInfo, walkSpeed, runSpeed, deathSpeed, attackPower, defensePower, attackSuccess, attackBlock,
            elementAttackPower, elementDefensePower, critical, followInfo, dropMoneyInfo, dropPotionInfo,
            dropItemInfo, dropQuestItemInfo, dropExtraItemInfo);
    }
}
