using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Movement;

public static class AvatarActionResumeWhitelist
{
    private const byte Type0 = 0b0000_0001;
    private const byte AllTypes = 0b1111_1111;

    private static readonly FrozenDictionary<int, byte> TypeMasksBySort = new Dictionary<int, byte>
    {
        [1] = AllTypes,
        [2] = AllTypes,
        [11] = AllTypes,
        [19] = Type0,
        [31] = Type0,
        [32] = AllTypes,
        [64] = Type0,
        [90] = AllTypes,
        [91] = AllTypes,
        [92] = AllTypes,
        [93] = AllTypes,
        [94] = AllTypes,
        [95] = AllTypes
    }.ToFrozenDictionary();

    public static bool IsLegal(int sort, int type)
    {
        return type is >= 0 and <= 7 && TypeMasksBySort.TryGetValue(sort, out var mask) &&
               (mask & (1 << type)) != 0;
    }
}
