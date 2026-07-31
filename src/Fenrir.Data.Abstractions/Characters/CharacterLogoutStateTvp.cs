using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterLogoutState")]
public sealed partial record CharacterLogoutStateTvp(
    int CharacterId,
    long FlushSequence,
    int LastZone,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);
