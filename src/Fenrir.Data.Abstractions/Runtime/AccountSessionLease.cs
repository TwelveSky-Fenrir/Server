using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateTvp(Schema = "runtime", TvpName = "tvp_AccountSessionLease")]
public sealed partial record AccountSessionLeaseTvp(int AccountId, Guid SessionToken);

[GenerateDto]
public sealed partial record HeldAccountSessionDto(int AccountId);
