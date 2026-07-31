using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterPosition")]
public sealed partial record CharacterPositionTvp(
    int CharacterId,
    long FlushSequence,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading);
