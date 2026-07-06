using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

// Ordinal-mapped: ctor order must match usp_AccountSession_ClaimOrSignalKick's SELECT order. Outcome is one
// of AccountSessionClaimOutcome's values; PreviousShardId is populated for ConflictGameKicked and (since
// Migrations/023_account_session_dead_shard_fast_reclaim.sql) ReclaimedDeadShard.
[GenerateDto]
public sealed partial record AccountSessionClaimDto(byte Outcome, byte? PreviousShardId);

// Ordinal-mapped: ctor order must match usp_AccountSession_RefreshAndGetKicked's SELECT order.
[GenerateDto]
public sealed partial record KickedAccountDto(int AccountId, Guid SessionToken);

// Ordinal-mapped: ctor order must match usp_AccountSession_ReapStale's SELECT order. ServerKind is one of
// AccountSessionServerKind's values -- the two distinct forced-timeout log paths the caller distinguishes on.
[GenerateDto]
public sealed partial record ReapedAccountSessionDto(int AccountId, byte ServerKind);
