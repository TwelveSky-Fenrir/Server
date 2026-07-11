using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     The real, SQL-backed <see cref="ITribePointRosterGateway" /> that replaces
///     <see cref="LoggingOnlyTribePointRosterGateway" />: scans the genuine persisted character/avatar roster
///     (game.Characters, via <see cref="ITribeRosterRepository" /> -&gt; game.usp_TribeRoster_GetForTribePoint,
///     Level &gt;= 145 only) and hands <see cref="TribePointLevelRecomputeService" /> the per-character
///     tribe/level/rebirth snapshots its pure formula needs. Sourcing from the avatar roster (not the legacy's
///     mis-targeted RankInfo table) is the whole point of this seam -- see
///     <see cref="ITribePointRosterGateway" />'s own remarks for the "known legacy bug, deliberately not
///     reproduced" citation trail.
/// </summary>
/// <remarks>
///     A failed read is surfaced as a thrown exception (from CaeriusNet), which
///     <see cref="TribePointLevelRecomputeService.RecomputeAsync" /> already catches and treats exactly like
///     the contract's "the per-tribe query cannot execute" case: abort the run, leave the previous totals
///     unchanged. An empty roster is a normal success (returns an empty list, from which the domain formula
///     still produces every tribe's 1000 baseline).
/// </remarks>
public sealed class SqlTribePointRosterGateway(ITribeRosterRepository roster) : ITribePointRosterGateway
{
    public async Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
    {
        var rows = await roster.GetForTribePointAsync(ct).ConfigureAwait(false);

        var snapshots = new TribeRosterCharacterSnapshot[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            snapshots[i] = new TribeRosterCharacterSnapshot(row.TribeId, row.Level1, row.Level2, row.RebirthCount);
        }

        return snapshots;
    }
}
