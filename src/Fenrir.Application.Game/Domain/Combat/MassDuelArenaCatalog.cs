using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

// Server/ts25zone/S04_MyWork04.cpp:1828, 1882, 1955, 1999 : le litteral 124 recopie aux quatre cases 600-603.
// Capacite de carte nommee, declaree une seule fois, plutot que quatre comparaisons d'entier dupliquees.
public static class MassDuelArenaCatalog
{
    private static readonly FrozenSet<short> ArenaMapIds =
        new[] { Zone124DuelOverrideResolver.Zone124MapId }.ToFrozenSet();

    public static bool IsMassDuelArena(short mapId)
    {
        return ArenaMapIds.Contains(mapId);
    }
}
