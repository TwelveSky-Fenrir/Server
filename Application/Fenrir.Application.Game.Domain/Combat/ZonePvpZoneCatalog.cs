using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

public static class ZonePvpZoneCatalog
{

        private static readonly FrozenSet<short> EnemyTribeAttackEnabledZoneIds = new short[]
    {
        1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14, 38, 49, 51, 53, 54, 55, 75, 84, 85, 86, 87, 88, 89, 90, 99,
        100, 120, 121, 122, 125, 138, 139, 140, 141, 142, 143, 146, 147, 148, 149, 150, 151, 152, 153, 154,
        155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 194, 195, 196, 197, 198, 199, 200, 201,
        250, 267, 268, 269, 270, 271, 272, 273, 274, 291, 295, 296, 297, 298, 299, 302, 303, 324, 335, 339,
        340, 344, 345
    }.ToFrozenSet();

        private static readonly FrozenSet<short> SameTribeAttackExemptZoneIds =
        new short[] { 324, PvpKillRewardZoneCatalog.FfaMapNumber }.ToFrozenSet();

        private static readonly FrozenSet<short> NewbieProtectionZoneIds =
        new short[] { 2, 3, 4, 7, 8, 9, 12, 13, 14 }.ToFrozenSet();

        public static bool AllowsEnemyTribeAttack(short zoneId)
    {
        return EnemyTribeAttackEnabledZoneIds.Contains(zoneId);
    }

        public static bool IsSameTribeAttackExempt(short zoneId)
    {
        return SameTribeAttackExemptZoneIds.Contains(zoneId);
    }

        public static bool IsNewbieProtectionZone(short zoneId)
    {
        return NewbieProtectionZoneIds.Contains(zoneId);
    }
}
