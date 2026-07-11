using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterRuneSocket")]
public sealed partial record CharacterRuneSocketTvp(
    byte SocketIndex,
    int RuneItemId,
    int RuneStat);
