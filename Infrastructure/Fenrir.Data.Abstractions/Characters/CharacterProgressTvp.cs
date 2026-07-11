using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

// Mirrors game.tvp_CharacterProgress order. FlushSequence shares CharacterPositionTvp's per-character monotonic counter; both PersistBatch procs enforce strictly-greater. Money excluded -- it only moves via usp_Character_AdjustMoney.
// EatLifePotion/EatManaPotion/EatStrPotion/EatDexPotion/EatElePotion appended last (Migrations/036, item-usage-consumables finding): the stat/elixir-potion lifetime counters ProgressWriteBehindHost now flushes alongside every other Vitals/Progression field.
// DropItemTime appended after those five (follow-up to the item-usage-consumables finding): the Lucky Drop/"Acquisition" Scroll minutes counter -- already mirrored into PlayerRuntimeState.DropItemTime, but never previously flushed back to game.Characters.DropItemTime.
// M15PetLuckyBoxPity appended last (Migrations/048, confirmation-pass follow-up): the M15 Pet Lucky Box (world.Items 8111) pity counter -- already mirrored into PlayerRuntimeState.M15PetLuckyBoxPity via LootBoxUseItemHandler, but never previously flushed back to game.Characters.M15PetLuckyBoxPity.
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
