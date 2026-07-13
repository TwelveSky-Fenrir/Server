using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Movement;

public static class CharacterMotionWhitelist
{
    private const byte Type0 = 0b0000_0001;
    private const byte Type3 = 0b0000_1000;
    private const byte Type5 = 0b0010_0000;
    private const byte Type7 = 0b1000_0000;
    private const byte AllTypes = 0b1111_1111;
    private const byte EvenTypes = 0b0101_0101;
    private const byte OddTypes = 0b1010_1010;

    private static readonly FrozenDictionary<int, Rule> Rules = new Dictionary<int, Rule>
    {
        [0] = new(Type0, 0, true, 0, 0),
        [1] = new(AllTypes, 0, true, 0, 0),
        [2] = new(AllTypes, 0, true, 0, 0),
        [3] = new(EvenTypes, 0, true, 0, 0),
        [4] = new(OddTypes, 0, true, 0, 0),
        [5] = new(OddTypes, 0, true, 1, 1),
        [6] = new(OddTypes, 0, true, 1, 2),
        [7] = new(OddTypes, 0, true, 1, 3),
        [9] = new(OddTypes, 0, true, 0, 0),
        [10] = new(AllTypes, 0, true, 0, 0),
        [13] = new(Type0, 0, true, 0, 0),
        [14] = new(Type0, 0, true, 0, 0),
        [15] = new(Type0, 0, true, 0, 0),
        [16] = new(Type0, 0, true, 0, 0),
        [17] = new(Type0, 0, true, 0, 0),
        [18] = new(Type0, 0, true, 0, 0),
        [20] = new(Type0, 0, true, 0, 0),
        [21] = new(Type0, 0, true, 0, 0),
        [22] = new(Type0, 0, true, 0, 0),
        [23] = new(Type0, 0, true, 0, 0),
        [30] = new(Type0, 1, true, 0, 0),
        [32] = new(AllTypes, 2, true, 0, 0),
        [33] = new(Type0, 2, true, 0, 0),
        [38] = new(Type0, 2, true, 2, 1),
        [39] = new(Type0, 2, true, 2, 1),
        [40] = new(Type0, 2, true, 0, 0),
        [41] = new(Type0, 2, true, 0, 0),
        [42] = new(Type3, 2, true, 3, 1),
        [43] = new(Type3, 2, true, 3, 3),
        [44] = new(Type3, 2, true, 3, 5),
        [45] = new(Type3, 2, true, 3, 1),
        [46] = new(Type3, 2, true, 3, 3),
        [48] = new(Type5, 2, true, 3, 1),
        [49] = new(Type5, 2, true, 3, 3),
        [50] = new(Type5, 2, true, 3, 5),
        [51] = new(Type5, 2, true, 3, 1),
        [52] = new(Type5, 2, true, 3, 3),
        [54] = new(Type7, 2, true, 4, 1),
        [55] = new(Type7, 2, true, 4, 3),
        [56] = new(Type7, 2, true, 3, 5),
        [57] = new(Type7, 2, true, 4, 1),
        [58] = new(Type7, 2, true, 4, 3),
        [60] = new(Type3, 2, true, 0, 0),
        [61] = new(Type5, 2, true, 0, 0),
        [62] = new(Type7, 2, true, 0, 0),
        [63] = new(Type0, 2, true, 0, 0),
        [64] = new(Type0, 0, true, 0, 0),
        [65] = new(Type0, 0, false, 5, 1),
        [66] = new(Type0, 2, true, 0, 0),
        [67] = new(Type0, 2, true, 0, 0),
        [68] = new(Type0, 2, true, 0, 0),
        [69] = new(Type3, 2, true, 3, 1),
        [70] = new(Type3, 2, true, 3, 3),
        [71] = new(Type5, 2, true, 3, 1),
        [72] = new(Type5, 2, true, 3, 3),
        [73] = new(Type7, 2, true, 4, 3),
        [74] = new(Type7, 2, false, 0, 0),
        [75] = new(Type0, 2, true, 0, 0),
        [76] = new(Type0, 2, true, 0, 0),
        [81] = new(Type3, 2, true, 3, 5),
        [82] = new(Type5, 2, true, 3, 5),
        [83] = new(Type7, 2, true, 3, 5),
        [85] = new(Type3, 2, true, 3, 1),
        [86] = new(Type3, 2, true, 3, 3),
        [87] = new(Type5, 2, true, 3, 1),
        [88] = new(Type5, 2, true, 3, 3),
        [89] = new(Type7, 2, true, 4, 1),
        [90] = new(Type7, 2, true, 4, 3),
        [91] = new(Type0, 0, true, 0, 0),
        [255] = new(AllTypes, 3, true, 0, 0)
    }.ToFrozenDictionary();

    public static bool TryEvaluate(int sort, int type, out CharacterMotionEvaluation evaluation)
    {
        if (type is >= 0 and <= 7 && Rules.TryGetValue(sort, out var rule) && (rule.TypeMask & (1 << type)) != 0)
        {
            evaluation = new CharacterMotionEvaluation(rule.SkillCategoryCode, rule.AttackBudgetEnforced,
                rule.AttackFamilyTag, rule.AttackSubPacketCeiling);
            return true;
        }

        evaluation = default;
        return false;
    }

    private readonly record struct Rule(
        byte TypeMask,
        int SkillCategoryCode,
        bool AttackBudgetEnforced,
        int AttackFamilyTag,
        int AttackSubPacketCeiling);
}
