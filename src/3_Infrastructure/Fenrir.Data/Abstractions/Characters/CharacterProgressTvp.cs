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
    int M15PetLuckyBoxPity);
