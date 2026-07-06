using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Runtime;

// Mirrors runtime.tvp_AccountIdList's single column -- batched input for usp_AccountSession_RefreshAndGetKicked's
// liveness/kick poll.
[GenerateTvp(Schema = "runtime", TvpName = "tvp_AccountIdList")]
public sealed partial record AccountIdTvp(int AccountId);
