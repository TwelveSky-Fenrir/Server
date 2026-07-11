namespace Fenrir.Application.Game.Domain.Consumables;

public static class ElementalPotionPacking
{
    public const int Modulus = 1000;

    public static int DamageSubValue(int raw)
    {
        return raw / Modulus;
    }

    public static int DefenseSubValue(int raw)
    {
        return raw % Modulus;
    }

    public static int WithDamageSubValue(int raw, int newDamageSubValue)
    {
        return newDamageSubValue * Modulus + DefenseSubValue(raw);
    }

    public static int WithDefenseSubValue(int raw, int newDefenseSubValue)
    {
        return DamageSubValue(raw) * Modulus + newDefenseSubValue;
    }
}
