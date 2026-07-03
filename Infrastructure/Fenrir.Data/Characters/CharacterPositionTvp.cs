using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

/// <summary>
///     Write-behind position row (architecture reference §10.5/§11.3) -- mirrors game.tvp_CharacterPosition's column
///     order 1:1. [GenerateTvp] emits an ITvpMapper that streams IEnumerable&lt;SqlDataRecord&gt; reusing a single
///     record instance across the whole batch, so flushing hundreds of players costs one round trip and next-to-no GC
///     pressure.
/// </summary>
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterPosition")]
public sealed partial record CharacterPositionTvp(
    int CharacterId,
    long FlushSequence,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading);
