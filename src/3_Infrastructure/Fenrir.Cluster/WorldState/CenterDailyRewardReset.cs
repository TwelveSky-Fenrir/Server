using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     CaeriusNet-backed daily (and Monday-weekly) reward-claim reset for the CenterServer, consumed by
///     <c>DailyResetHost</c>. Routes the reset through <c>game.usp_Character_ResetDailyRewardClaims</c> instead
///     of a hard-coded member table name, correcting legacy Bug 4.
/// </summary>
public sealed record CenterDailyRewardReset(ICaeriusNetDbContext Db) : ICenterDailyRewardReset
{
    public ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_ResetDailyRewardClaims", 0)
            .AddParameter("ClearWeeklyDayCounter", clearWeeklyDayCounter, SqlDbType.Bit)
            .Build();

        return Db.ExecuteAsync(sp, ct);
    }
}
