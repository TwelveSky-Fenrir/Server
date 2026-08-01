using Fenrir.Application.Game.Domain.Buffs;

namespace Fenrir.Application.Game.Domain.Combat;

public static class HolyShieldResolver
{
    public const int BaseSlot = BuffCatalog.HolyShield;

    public static readonly int[] TieredSlots = BuffCatalog.HolyShieldCapeSlots;

    public static int Absorb(int[] buff, int incomingDamage)
    {
        if (incomingDamage <= 0)
            return 0;

        var slot = ResolveActiveSlot(buff);
        if (slot < 0)
            return 0;

        var value = buff[slot * 2];
        var absorbed = Math.Min(incomingDamage, value);
        var remaining = value - absorbed;
        buff[slot * 2] = remaining;
        if (remaining <= 0)
            buff[slot * 2 + 1] = 0;
        return absorbed;
    }

    public static bool RemoveAll(int[] buff)
    {
        var any = ClearSlot(buff, BaseSlot);
        foreach (var slot in TieredSlots)
            any |= ClearSlot(buff, slot);
        return any;
    }

    private static int ResolveActiveSlot(int[] buff)
    {
        return buff[BaseSlot * 2] > 0 ? BaseSlot : -1;
    }

    private static bool ClearSlot(int[] buff, int slot)
    {
        if (buff[slot * 2] == 0 && buff[slot * 2 + 1] == 0)
            return false;

        buff[slot * 2] = 0;
        buff[slot * 2 + 1] = 0;
        return true;
    }
}
