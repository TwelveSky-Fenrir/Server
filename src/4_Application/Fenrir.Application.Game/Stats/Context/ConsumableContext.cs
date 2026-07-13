namespace Fenrir.Application.Game.Stats.Context;

public readonly record struct ConsumableContext(
    int EatLifePotion = 0,
    int EatManaPotion = 0,
    int EatStrPotion = 0,
    int EatDexPotion = 0,
    int EatElePotion = 0,
    bool HpBoostActive = false,
    bool WarriorPillActive = false,
    int MaxPotionEventNum = 0,
    byte EventTribe = 0);
