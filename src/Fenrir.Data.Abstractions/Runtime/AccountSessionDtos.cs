using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateDto]
public sealed partial record AccountSessionClaimDto(byte Outcome, byte? PreviousShardId);

[GenerateDto]
public sealed partial record KickedAccountDto(int AccountId, Guid SessionToken);

[GenerateDto]
public sealed partial record ReapedAccountSessionDto(int AccountId, byte ServerKind);
