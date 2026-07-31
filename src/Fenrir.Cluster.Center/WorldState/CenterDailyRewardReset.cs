using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Cluster.Center.WorldState;

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
