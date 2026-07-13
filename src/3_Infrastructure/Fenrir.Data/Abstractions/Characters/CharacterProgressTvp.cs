using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterProgress")]
public sealed partial record CharacterProgressTvp(
    int CharacterId,
    long FlushSequence,
    short Level,
    short Level2,
    long Experience,
    int Life,
    int MaxLife,
    int Mana,
    int MaxMana,
    int StatVit,
    int StatStr,
    int StatInt,
    int StatDex,
    int StatPoints,
    int SkillPoints,
    int ContributionPoints,
    int Exp2,
    int RebirthCount,
    int EatLifePotion,
    int EatManaPotion,
    int EatStrPotion,
    int EatDexPotion,
    int EatElePotion,
    int DropItemTime,
    int M15PetLuckyBoxPity,
    // Mount block appended last: the single persisted mount (garage slot 0) re-encoded from PlayerRuntimeState
    // on each flush -- MountExpActivity packs activity/accumulated-exp, MountPower packs the 8 rolled digits,
    // MountSlotIndex is the aAnimalIndex pointer. In-session mount play (activity spend, exp gain, attribute
    // rolls) mutates these in memory; without them here the flush would silently revert a live mount back to
    // its last-persisted value, same reasoning as the DropItemTime/Eat*Potion counters above.
    int MountItemId,
    int MountExpActivity,
    int MountPower,
    int MountSlotIndex,
    int MountTime);
