using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

/// <summary>
///     Write-behind progression row (D7 regime (a): xp/level/vitals/points on the 5 s flush cadence) -- mirrors
///     game.tvp_CharacterProgress's column order 1:1. FlushSequence is the SAME per-character monotonic counter
///     <see cref="CharacterPositionTvp" /> carries: the write-behind host mints one sequence per character for every
///     flavor of dirty state, and both PersistBatch procs apply the identical strictly-greater guard. Money is
///     deliberately absent -- it only moves through usp_Character_AdjustMoney (regime (b)).
/// </summary>
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
