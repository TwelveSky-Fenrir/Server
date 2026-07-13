using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static readonly FrozenDictionary<int, MountBaseRow> MountBaseDataByItemId =
        new Dictionary<int, MountBaseRow>
        {
            [1301] = new(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),
            [8301] = new(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),
            [7001] = new(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),

            [1302] = new(5, 5, 0, 0, 0, 0, 0, 0, 5, 30, 1, 27),
            [8302] = new(5, 5, 0, 0, 0, 0, 0, 0, 5, 30, 1, 27),

            [1303] = new(0, 5, 0, 0, 0, 0, 5, 5, 0, 30, 2, 30),
            [8303] = new(0, 5, 0, 0, 0, 0, 5, 5, 0, 30, 2, 30),

            [1304] = new(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [8304] = new(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [559] = new(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [17044] = new(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),

            [1305] = new(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),
            [8305] = new(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),
            [17045] = new(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),

            [1306] = new(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),
            [8306] = new(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),
            [17046] = new(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),

            [1307] = new(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),
            [8307] = new(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),
            [685] = new(0, 15, 15, 0, 0, 0, 0, 15, 0, 0, 6, -1),
            [814] = new(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),

            [1308] = new(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),
            [8308] = new(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),
            [683] = new(15, 15, 0, 0, 0, 0, 0, 0, 15, 0, 7, -1),
            [819] = new(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),

            [1309] = new(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),
            [8309] = new(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),
            [817] = new(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),

            [1313] = new(0, 5, 5, 0, 5, 0, 0, 0, 0, 30, 9, 33),
            [8313] = new(0, 5, 5, 0, 5, 0, 0, 0, 0, 30, 9, 33),
            [1451] = new(0, 5, 5, 0, 5, 0, 0, 0, 0, 0, 9, -1),

            [1314] = new(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),
            [8314] = new(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),
            [17047] = new(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),

            [1315] = new(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),
            [8315] = new(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),
            [820] = new(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),

            [1316] = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, -1),
            [8316] = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, -1),
            [510] = new(0, 0, 10, 0, 10, 0, 15, 0, 0, 0, 12, -1),
            [511] = new(0, 0, 15, 0, 15, 0, 15, 0, 0, 0, 12, -1),

            [1317] = new(0, 5, 5, 0, 0, 0, 0, 0, 5, 30, 13, 36),
            [8317] = new(0, 5, 5, 0, 0, 0, 0, 0, 5, 30, 13, 36),

            [1318] = new(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),
            [8318] = new(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),
            [17048] = new(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),

            [1319] = new(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),
            [8319] = new(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),
            [818] = new(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),

            [1320] = new(5, 5, 0, 0, 0, 0, 0, 5, 0, 30, 16, 42),
            [8320] = new(5, 5, 0, 0, 0, 0, 0, 5, 0, 30, 16, 42),

            [1321] = new(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),
            [8321] = new(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),
            [17049] = new(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),

            [1322] = new(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [8322] = new(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [684] = new(15, 15, 0, 0, 0, 0, 0, 15, 0, 0, 18, -1),
            [821] = new(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [17058] = new(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),

            [1323] = new(0, 5, 0, 0, 0, 5, 5, 0, 0, 30, 19, 39),
            [8323] = new(0, 5, 0, 0, 0, 5, 5, 0, 0, 30, 19, 39),

            [1324] = new(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),
            [8324] = new(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),
            [17050] = new(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),

            [1325] = new(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),
            [8325] = new(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),
            [815] = new(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),

            [1326] = new(0, 5, 0, 5, 0, 5, 0, 0, 0, 30, 22, 45),
            [8326] = new(0, 5, 0, 5, 0, 5, 0, 0, 0, 30, 22, 45),

            [1327] = new(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),
            [8327] = new(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),
            [17051] = new(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),

            [1328] = new(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),
            [8328] = new(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),
            [816] = new(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),

            [1329] = new(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),
            [8329] = new(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),
            [17059] = new(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),

            [1330] = new(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),
            [8330] = new(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),
            [17060] = new(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),

            [1331] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),
            [8331] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),
            [17061] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),

            [1332] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 48, 48),
            [1333] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 49, 48),
            [1334] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 50, 48),
            [1335] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 51, 48),
            [1336] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 52, 48),
            [1337] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 53, 48),
            [1338] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 54, 48),
            [1339] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 55, 48),
            [1340] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 56, 48),
            [1341] = new(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 57, 48)
        }.ToFrozenDictionary();


    private static readonly FrozenDictionary<int, float> MountGradeMultiplierByColumn =
        new Dictionary<int, float>
        {
            [5] = 1.05f,
            [10] = 1.10f,
            [15] = 1.15f,
            [20] = 1.20f
        }.ToFrozenDictionary();

    private static readonly FrozenSet<int> MountGradeThreeTierColumns = new[] { 5, 10, 15 }.ToFrozenSet();

    private static readonly FrozenDictionary<MountFlatStat, int> MountFlatBonusPerPointByStat =
        new Dictionary<MountFlatStat, int>
        {
            [MountFlatStat.MaxLife] = 100,
            [MountFlatStat.MaxMana] = 200,
            [MountFlatStat.Attack] = 50,
            [MountFlatStat.Defense] = 100,
            [MountFlatStat.Hit] = 100,
            [MountFlatStat.Dodge] = 100,
            [MountFlatStat.ElementAttack] = 50,
            [MountFlatStat.ElementDefense] = 50
        }.ToFrozenDictionary();

    public static bool TryGetMountBaseRow(int mountItemId, out MountBaseRow row)
    {
        return MountBaseDataByItemId.TryGetValue(mountItemId, out row);
    }

    public static int ApplyMountGradeMultiplierFourTier(int runningTotal, int column)
    {
        return MountGradeMultiplierByColumn.TryGetValue(column, out var multiplier)
            ? (int)(runningTotal * multiplier)
            : runningTotal;
    }

    public static int ApplyMountGradeMultiplierThreeTier(int runningTotal, int column)
    {
        return MountGradeThreeTierColumns.Contains(column)
            ? (int)(runningTotal * MountGradeMultiplierByColumn[column])
            : runningTotal;
    }

    private static int MountFlatPerPoint(MountFlatStat stat, int gradeDigit)
    {
        return gradeDigit > 0 ? MountFlatBonusPerPointByStat[stat] * gradeDigit : 0;
    }

    public static int MountFlatMaxLifeBonus(int hpGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.MaxLife, hpGradeDigit);
    }

    public static int MountFlatMaxManaBonus(int mpGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.MaxMana, mpGradeDigit);
    }

    public static int MountFlatAttackBonus(int attackGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Attack, attackGradeDigit);
    }

    public static int MountFlatDefenseBonus(int defenseGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Defense, defenseGradeDigit);
    }

    public static int MountFlatHitBonus(int hitGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Hit, hitGradeDigit);
    }

    public static int MountFlatDodgeBonus(int dodgeGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Dodge, dodgeGradeDigit);
    }

    public static int MountFlatElementAttackBonus(int elementAttackGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.ElementAttack, elementAttackGradeDigit);
    }

    public static int MountFlatElementDefenseBonus(int elementDefenseGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.ElementDefense, elementDefenseGradeDigit);
    }

    public static MountPowerDigits DecodeMountPowerDigits(int power, int activity)
    {
        return activity > 0
            ? new MountPowerDigits(
                power / 100_000 % 10,
                power / 10_000 % 10,
                power / 10_000_000 % 10,
                power / 1_000_000 % 10,
                power / 1_000 % 10,
                power / 100 % 10,
                power / 10 % 10,
                power % 10)
            : default;
    }

    public static MountFlatBonuses ComputeMountFlatBonuses(int power, int activity)
    {
        var digits = DecodeMountPowerDigits(power, activity);
        return new MountFlatBonuses(
            MountFlatMaxLifeBonus(digits.MaxLife),
            MountFlatMaxManaBonus(digits.MaxMana),
            MountFlatAttackBonus(digits.Attack),
            MountFlatDefenseBonus(digits.Defense),
            MountFlatHitBonus(digits.Hit),
            MountFlatDodgeBonus(digits.Dodge),
            MountFlatElementAttackBonus(digits.ElementAttack),
            MountFlatElementDefenseBonus(digits.ElementDefense));
    }


    public static int MountAbsorbPrimaryBonus(MountContext mount)
    {
        return mount.AnimalNumber != 0 && mount.AbsorbActive ? mount.AbsorbValue : 0;
    }


    public readonly record struct MountBaseRow(
        int MaxLifeColumn,
        int MaxManaColumn,
        int AttackColumn,
        int DefenseColumn,
        int HitColumn,
        int DodgeColumn,
        int CriticalColumn,
        int ElementAttackColumn,
        int ElementDefenseColumn,
        int AbsorbValue,
        int ModelId,
        int AbilityEffectId);


    private enum MountFlatStat
    {
        MaxLife,
        MaxMana,
        Attack,
        Defense,
        Hit,
        Dodge,
        ElementAttack,
        ElementDefense
    }


    public readonly record struct MountPowerDigits(
        int MaxLife = 0,
        int MaxMana = 0,
        int Attack = 0,
        int Defense = 0,
        int Hit = 0,
        int Dodge = 0,
        int ElementAttack = 0,
        int ElementDefense = 0);

    public readonly record struct MountFlatBonuses(
        int MaxLife = 0,
        int MaxMana = 0,
        int Attack = 0,
        int Defense = 0,
        int Hit = 0,
        int Dodge = 0,
        int ElementAttack = 0,
        int ElementDefense = 0);
}
