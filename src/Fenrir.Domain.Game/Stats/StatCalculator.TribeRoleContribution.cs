using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static readonly FrozenSet<short> TribeRoleSuppressedZones = ((short[])[124]).ToFrozenSet();

    private static int TribeRoleAttackDefenseBonus(ZoneContext zone)
    {
        if (TribeRoleSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return (TribeRoleKind)zone.TribeRole switch
        {
            TribeRoleKind.Master => 200,
            TribeRoleKind.SubMaster => 100,
            _ => 0
        };
    }


    public static int TribeRoleAttackPowerBonus(ZoneContext zone)
    {
        return TribeRoleAttackDefenseBonus(zone);
    }

    public static int TribeRoleDefensePowerBonus(ZoneContext zone)
    {
        return TribeRoleAttackDefenseBonus(zone);
    }

    public static int TribeRoleCriticalBonus(ZoneContext zone)
    {
        if (TribeRoleSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return (TribeRoleKind)zone.TribeRole == TribeRoleKind.Master ? 2 : 0;
    }

    private enum TribeRoleKind : byte
    {
        None = 0,
        Master = 1,
        SubMaster = 2,
        Council = 3
    }
}
