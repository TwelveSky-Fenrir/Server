using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

// Mirrors game.tvp_CharacterRuneSocket order; CharacterId is a proc scalar, not a TVP column -- the replace
// unit is the whole per-character 4-socket rune array (legacy aRuneSystem[4]/aRuneSystemStat[4] save block).
// Only OCCUPIED sockets are carried (row absence = empty socket); an all-empty rune state sends zero rows and
// the caller omits the TVP entirely (SQL Server rejects a zero-row TVP).
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterRuneSocket")]
public sealed partial record CharacterRuneSocketTvp(
    byte SocketIndex,
    int RuneItemId,
    int RuneStat);
