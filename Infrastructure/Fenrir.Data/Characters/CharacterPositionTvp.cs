using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

// Mirrors game.tvp_CharacterPosition column order; GenerateTvp streams one reused SqlDataRecord per batch (1 round trip, minimal GC).
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterPosition")]
public sealed partial record CharacterPositionTvp(
    int CharacterId,
    long FlushSequence,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading);
