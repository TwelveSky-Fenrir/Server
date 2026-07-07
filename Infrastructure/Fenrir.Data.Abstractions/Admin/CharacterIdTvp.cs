using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Admin;

// Mirrors admin.tvp_CharacterIdList's single column -- batched input for
// usp_Mute_GetActiveForCharacters' MuteRefreshPollHost poll.
[GenerateTvp(Schema = "admin", TvpName = "tvp_CharacterIdList")]
public sealed partial record CharacterIdTvp(int CharacterId);
