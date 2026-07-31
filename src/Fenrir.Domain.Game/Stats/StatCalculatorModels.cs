namespace Fenrir.Domain.Game.Stats;

public readonly record struct EquippedItemSlot(
    int SlotIndex,
    ItemRowDto Item,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1 = 0,
    int SocketGem2 = 0,
    int SocketGem3 = 0)
{
    // Les 4 octets de aEquip[i][2] dans l'ordre ou SetISIUIMValue les empile : IS/IU/IM/IZ
    // (Server/Header/function.h:415-425 ; noms Server/ts25login/S04_MyWork02.cpp:1110).
    public int PackedUpgradeValue => Enchant | (Combine << 8) | (Refine << 16) | (Socket << 24);
}

public readonly record struct CharacterBaseAttributes(
    int Vitality,
    int Strength,
    int Intelligence,
    int Dexterity,
    short Level,
    byte Tribe,
    byte PreviousTribe,
    int Title,
    int Halo,
    int RebirthCount,
    short Level2 = 0)
{
    public short CombinedLevel => (short)(Level + Level2);
}

public readonly record struct PetStatContribution(
    int Life = 0,
    int Mana = 0,
    int AttackPower = 0,
    int DefensePower = 0,
    int SteppedAttackBonus = 0);

public readonly record struct EffectiveStats(
    int MaxLife,
    int MaxMana,
    int AttackPower,
    int DefensePower,
    int AttackSuccess,
    int AttackBlock,
    int Critical,
    int CriticalDefence,
    int Luck,
    int ElementAttackPower,
    int ElementDefensePower);
