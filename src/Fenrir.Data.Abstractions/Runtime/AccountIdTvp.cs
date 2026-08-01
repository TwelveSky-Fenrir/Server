using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateTvp(Schema = "runtime", TvpName = "tvp_AccountIdList")]
public sealed partial record AccountIdTvp(int AccountId);
