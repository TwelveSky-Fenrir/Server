using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

// Ordinal-mapped: ctor order must match usp_GameServer_GetDirectory's SELECT order.
[GenerateDto]
public sealed partial record ShardDirectoryEntryDto(
    byte ShardId,
    string Host,
    int Port,
    int Ccu,
    int Capacity,
    float TickP99Ms);

/// <summary>
///     Single C#-side source of truth for <c>runtime.usp_GameServer_GetDirectory</c>'s staleness window --
///     a row whose <c>LastHeartbeatUtc</c> is older than this many seconds is silently excluded from the
///     directory read. Previously a bare literal (<c>-15</c>) baked directly into the .sql file with nothing
///     anywhere validating a shard's own <c>Game:HeartbeatIntervalSeconds</c> against it; the procedure now
///     takes <c>@StalenessCutoffSeconds</c> as an explicit parameter defaulting to this same value (see
///     Database/Migrations/021_gameserver_directory_configurable_staleness_cutoff.sql) so the number lives in
///     exactly one place instead of being duplicated as an unreferenced comment.
/// </summary>
/// <remarks>
///     A shard's heartbeat interval must stay comfortably under this cutoff or its own directory row can
///     intermittently (or permanently) fail this filter despite the shard being perfectly healthy -- closing
///     that half of the gap (a boot-time check on <c>Game:HeartbeatIntervalSeconds</c> against this constant)
///     belongs in <c>Application.Fenrir.Application.Game.Domain.GameServerOptionsValidator</c>, outside this
///     project's scope.
/// </remarks>
public static class GameServerDirectoryDefaults
{
    public const int StalenessCutoffSeconds = 15;
}
