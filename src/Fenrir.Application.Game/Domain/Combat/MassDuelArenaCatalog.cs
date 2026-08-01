using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

public static class MassDuelArenaCatalog
{
    private static readonly FrozenSet<short> ArenaMapIds =
        new[] { Zone124DuelOverrideResolver.Zone124MapId }.ToFrozenSet();

    public static bool IsMassDuelArena(short mapId)
    {
        return ArenaMapIds.Contains(mapId);
    }
}
