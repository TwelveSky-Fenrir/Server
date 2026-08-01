namespace Fenrir.Application.Game.Domain.Buffs;

public enum BuffValueSemantic
{
    Unused,

    PercentBonus,

    Probability,

    FlatAmount,

    AbsorbPool,

    ClientVisualOnly,

    ViewOnlyFlag
}

public static class BuffCatalog
{
    public const int SlotCount = 35;

    public const int AttackPower = 0;
    public const int DefensePower = 1;
    public const int AttackSuccess = 2;
    public const int AttackBlock = 3;
    public const int ElementAttackPower = 4;
    public const int ElementDefensePower = 5;
    public const int AttackSpeed = 6;
    public const int MoveSpeed = 7;
    public const int Charge = 8;
    public const int HolyShield = 9;
    public const int Critical = 10;
    public const int Luck = 11;
    public const int Reflect = 12;
    public const int StunBlock = 13;
    public const int HolyShieldStrip = 14;
    public const int DarkAttack = 15;
    public const int DarkAttackPotionDebuff = 16;
    public const int HitRatePotion = 17;
    public const int DodgeRatePotion = 18;

    public const int FirstReservedSlot = 19;
    public const int LastReservedSlot = 28;

    public const int HolyShieldCapeFirst = 29;
    public const int HolyShieldCapeLast = 34;

    public const int DarkAttackRollBase = 100;
    public const int HolyShieldStripRollBase = 1000;
    public const int ReflectRollBase = 1500;

    // Inertes : jamais alimentes sous M33/LNW33, seul ecrivain sous MG5ORIGIN_ECAPE non defini (Server/ts25zone/S07_MyGame03.cpp:8726).
    public static readonly int[] HolyShieldCapeSlots =
        [29, 30, 31, 32, 33, 34];

    public static readonly int[] HolyShieldSlots =
        [HolyShield, .. HolyShieldCapeSlots];

    public static readonly int[] ExclusivityFlagSlots =
        [DarkAttack, HitRatePotion, DodgeRatePotion];

    public static bool IsDurationCounted(int slot)
    {
        return slot != DarkAttackPotionDebuff;
    }

    public static bool ClearsExclusivityFlagOnExpiry(int slot)
    {
        return slot is DarkAttack or HitRatePotion or DodgeRatePotion;
    }

    public static bool IsHolyShieldSlot(int slot)
    {
        return slot == HolyShield || slot is >= HolyShieldCapeFirst and <= HolyShieldCapeLast;
    }

    public static bool IsWrittenByShippedBuild(int slot)
    {
        return slot is >= AttackPower and <= DodgeRatePotion && slot != ElementDefensePower;
    }

    public static int ProbabilityRollBase(int slot)
    {
        return slot switch
        {
            DarkAttack => DarkAttackRollBase,
            HolyShieldStrip => HolyShieldStripRollBase,
            Reflect => ReflectRollBase,
            _ => 0
        };
    }

    public static BuffValueSemantic SemanticOf(int slot)
    {
        return slot switch
        {
            AttackPower or DefensePower or AttackSuccess or AttackBlock => BuffValueSemantic.PercentBonus,
            ElementAttackPower or ElementDefensePower => BuffValueSemantic.PercentBonus,
            Critical or Luck or StunBlock => BuffValueSemantic.PercentBonus,
            HitRatePotion or DodgeRatePotion => BuffValueSemantic.PercentBonus,
            AttackSpeed or MoveSpeed => BuffValueSemantic.ClientVisualOnly,
            Charge => BuffValueSemantic.FlatAmount,
            HolyShield => BuffValueSemantic.AbsorbPool,
            Reflect or HolyShieldStrip or DarkAttack => BuffValueSemantic.Probability,
            DarkAttackPotionDebuff => BuffValueSemantic.ViewOnlyFlag,
            >= HolyShieldCapeFirst and <= HolyShieldCapeLast => BuffValueSemantic.AbsorbPool,
            _ => BuffValueSemantic.Unused
        };
    }

    public static string NameOf(int slot)
    {
        return slot switch
        {
            AttackPower => "AttackPower",
            DefensePower => "DefensePower",
            AttackSuccess => "AttackSuccess",
            AttackBlock => "AttackBlock",
            ElementAttackPower => "ElementAttackPower",
            ElementDefensePower => "ElementDefensePower",
            AttackSpeed => "AttackSpeed",
            MoveSpeed => "MoveSpeed",
            Charge => "Charge",
            HolyShield => "HolyShield",
            Critical => "Critical",
            Luck => "Luck",
            Reflect => "Reflect",
            StunBlock => "StunBlock",
            HolyShieldStrip => "HolyShieldStrip",
            DarkAttack => "DarkAttack",
            DarkAttackPotionDebuff => "DarkAttackPotionDebuff",
            HitRatePotion => "HitRatePotion",
            DodgeRatePotion => "DodgeRatePotion",
            >= HolyShieldCapeFirst and <= HolyShieldCapeLast => "HolyShieldCapeTier",
            _ => "Reserved"
        };
    }
}
