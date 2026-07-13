namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The daily reward-claim reset seam (Bug 4 fix). DECLARED here and consumed by <c>DailyResetHost</c>; the
///     implementation is owned by Main / the data unit and must resolve the member/account table through the
///     normal repository + parameterised stored procedure, NEVER a literal table name.
/// </summary>
/// <remarks>
///     CORRECTS legacy Bug 4: the daily reset wrote to a hard-coded <c>MemberInfo</c> table name
///     (Server/ts25center/S07_MyGame01.cpp:230,233), bypassing the configurable table-slot indirection every
///     other statement in the subsystem uses. The correct behaviour routes through a repository/proc so a
///     repointed member table stays consistent with the rest of the system.
///     <para>
///         A matching stored procedure did not exist in the current schema (flagged): it must clear the
///         reward-claim state for all members, and additionally clear the reward-claim day counter when
///         <paramref name="clearWeeklyDayCounter" /> is set (the Monday variant).
///     </para>
/// </remarks>
public interface ICenterDailyRewardReset
{
    ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct);
}
