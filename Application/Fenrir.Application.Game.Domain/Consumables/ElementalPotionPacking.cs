namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     wAvatar.aEatElePotion packs two independent sub-values into one stored counter: an elemental-damage
///     sub-value (the counter divided by 1000) and an elemental-defense sub-value (the counter modulo 1000).
///     Every Elemental-family stat-potion id (ordinary single/ten-unit and g12 tiers alike) mutates one of
///     these two sub-values, never both -- this type is the single place that encodes the pack/unpack
///     convention, so <see cref="StatPotionResolver" /> itself can stay kind-agnostic (it only ever operates
///     on an already-unpacked sub-value in the 0-400 range, same as the four non-Elemental counters).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:9132-9139 (<c>GetEatElePotion</c> -- the divide-by-1000/
///     modulo-1000 packed-dual-value read) ; Server/ts25zone/S04_MyWork03.cpp:2915-2947 (the elemental-specific
///     tAddTime/tAddTime2 pairs that establish which of the two sub-values a given id mutates and by how much:
///     578 1/1000 -- single-unit, damage-only, since 1000 is an exact multiple of the modulus and therefore
///     never perturbs the defense remainder ; 579 1/1 -- single-unit, defense-only ; 640 10/10000 -- ten-unit,
///     damage-only ; 641 10/10 -- ten-unit, defense-only).
/// </remarks>
public static class ElementalPotionPacking
{
    /// <summary>The packing modulus -- also the maximum a single sub-value could ever occupy before colliding with the next.</summary>
    public const int Modulus = 1000;

    /// <summary>The elemental-damage sub-value (raw counter divided by <see cref="Modulus" />).</summary>
    public static int DamageSubValue(int raw)
    {
        return raw / Modulus;
    }

    /// <summary>The elemental-defense sub-value (raw counter modulo <see cref="Modulus" />).</summary>
    public static int DefenseSubValue(int raw)
    {
        return raw % Modulus;
    }

    /// <summary>Repacks <paramref name="raw" /> with a new damage sub-value, leaving the defense sub-value untouched.</summary>
    public static int WithDamageSubValue(int raw, int newDamageSubValue)
    {
        return newDamageSubValue * Modulus + DefenseSubValue(raw);
    }

    /// <summary>Repacks <paramref name="raw" /> with a new defense sub-value, leaving the damage sub-value untouched.</summary>
    public static int WithDefenseSubValue(int raw, int newDefenseSubValue)
    {
        return DamageSubValue(raw) * Modulus + newDefenseSubValue;
    }
}
