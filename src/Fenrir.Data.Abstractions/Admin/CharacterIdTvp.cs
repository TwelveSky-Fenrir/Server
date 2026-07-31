using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Admin;

[GenerateTvp(Schema = "admin", TvpName = "tvp_CharacterIdList")]
public sealed partial record CharacterIdTvp(int CharacterId);
