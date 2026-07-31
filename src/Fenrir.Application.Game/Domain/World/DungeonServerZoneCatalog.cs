using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World;

public static class DungeonServerZoneCatalog
{
    private static readonly FrozenSet<short> DungeonServerNumbers = new short[]
    {
        46, 16, 17, 18, 40, 62, 63, 64, 59, 101, 104, 105, 110, 111, 251, 252, 259, 260, 126, 127, 128, 129,
        210, 211, 212, 47, 22, 23, 24, 41, 65, 66, 67, 60, 102, 106, 107, 112, 113, 253, 254, 261, 262, 130,
        131, 132, 133, 213, 214, 215, 48, 28, 29, 30, 42, 68, 69, 70, 61, 103, 108, 109, 114, 115, 255, 256,
        263, 264, 134, 135, 136, 137, 216, 217, 218, 167, 168, 169, 116, 117, 257, 258, 265, 266, 171, 172,
        173, 174, 219, 220, 221, 39, 144, 145, 313, 74
    }.ToFrozenSet();

    public static bool IsDungeonServer(short mapId)
    {
        return DungeonServerNumbers.Contains(mapId);
    }
}
