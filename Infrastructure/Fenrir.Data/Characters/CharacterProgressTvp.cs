using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

// Mirrors game.tvp_CharacterProgress order. FlushSequence shares CharacterPositionTvp's per-character monotonic counter; both PersistBatch procs enforce strictly-greater. Money excluded -- it only moves via usp_Character_AdjustMoney.
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
    int ContributionPoints);
