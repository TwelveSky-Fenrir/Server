using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>Parses <c>005_00003.IMG</c> (<c>SKILL_INFO</c>, STRUCT.h:123-146); no known runtime patches, unlike <see cref="ItemReader" />.</summary>
internal static class SkillReader
{
    private const string FileName = "005_00003.IMG";
    private const int XorKey = 0x10B6;
    private const int RecordArrayOffset = 84;
    private const int RecordCount = 300;
    private const int RecordSize = 776;

    public static IReadOnlyList<SkillRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var skills = new List<SkillRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            skills.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return skills;
    }

    public static IReadOnlyList<SkillRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static SkillRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var name = reader.ReadFixedString(25);
        var description = new string[10];
        for (var i = 0; i < 10; i++) description[i] = reader.ReadFixedString(51);
        reader.Skip(1); // compiler padding before sType (offset 539 -> 540)

        var type = reader.ReadInt32();
        var attackType = reader.ReadInt32();
        var dataNumber2D = reader.ReadInt32();
        var tribeInfo = reader.ReadInt32Array(2);
        var learnSkillPoint = reader.ReadInt32();
        var maxUpgradePoint = reader.ReadInt32();
        var totalHitNumber = reader.ReadInt32();
        var validRadius = reader.ReadInt32();
        var gradeInfo = new SkillGradeRecord[2];
        for (var i = 0; i < 2; i++) gradeInfo[i] = ReadGradeInfo(ref reader);

        return new SkillRecord(
            index, name, description, type, attackType, dataNumber2D, tribeInfo, learnSkillPoint, maxUpgradePoint,
            totalHitNumber, validRadius, gradeInfo);
    }

    private static SkillGradeRecord ReadGradeInfo(ref LegacySpanReader reader)
    {
        var manaUse = reader.ReadInt32();
        var recoverInfo = reader.ReadInt32Array(2);
        var stunAttack = reader.ReadInt32();
        var stunDefense = reader.ReadInt32();
        var fastRunSpeed = reader.ReadInt32();
        var attackInfo = reader.ReadInt32Array(3);
        var runTime = reader.ReadInt32();
        var chargingDamageUp = reader.ReadInt32();
        var attackPowerUp = reader.ReadInt32();
        var defensePowerUp = reader.ReadInt32();
        var attackSuccessUp = reader.ReadInt32();
        var attackBlockUp = reader.ReadInt32();
        var elementAttackUp = reader.ReadInt32();
        var elementDefenseUp = reader.ReadInt32();
        var attackSpeedUp = reader.ReadInt32();
        var runSpeedUp = reader.ReadInt32();
        var shieldLifeUp = reader.ReadInt32();
        var luckUp = reader.ReadInt32();
        var criticalUp = reader.ReadInt32();
        var returnSuccessUp = reader.ReadInt32();
        var stunDefenseUp = reader.ReadInt32();
        var destroySuccessUp = reader.ReadInt32();

        return new SkillGradeRecord(
            manaUse, recoverInfo, stunAttack, stunDefense, fastRunSpeed, attackInfo, runTime, chargingDamageUp,
            attackPowerUp, defensePowerUp, attackSuccessUp, attackBlockUp, elementAttackUp, elementDefenseUp,
            attackSpeedUp, runSpeedUp, shieldLifeUp, luckUp, criticalUp, returnSuccessUp, stunDefenseUp,
            destroySuccessUp);
    }
}
