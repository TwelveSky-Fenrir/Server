using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Progression;

public static class AutoHuntEnableGate
{
    public static readonly FrozenSet<short> BlockedMapNumbers = new short[]
    {
        38, 319, 320, 321, 322, 323,
        241, 242, 243, 244, 245, 246, 247, 248, 249,
        292, 293, 294,
        311, 312,
        325, 326, 327, 328, 329, 330
    }.ToFrozenSet();

    public static bool IsEnableBlocked(short mapId)
    {
        return BlockedMapNumbers.Contains(mapId);
    }
}
