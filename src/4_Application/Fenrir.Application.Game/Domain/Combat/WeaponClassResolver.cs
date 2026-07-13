namespace Fenrir.Application.Game.Domain.Combat;

public static class WeaponClassResolver
{
    public static int GetActionType(byte? weaponSort)
    {
        return GetWeaponClass(weaponSort) * 2;
    }

    public static int GetWeaponClass(byte? weaponSort)
    {
        return weaponSort switch
        {
            13 or 17 or 19 => 1,
            14 or 16 or 20 => 2,
            15 or 18 or 21 => 3,
            _ => 0
        };
    }
}
