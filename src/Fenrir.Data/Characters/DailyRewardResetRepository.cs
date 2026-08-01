using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record DailyRewardResetRepository(ICaeriusNetDbContext Db) : IDailyRewardResetRepository
{
    public ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_ResetAll", 0)
            .AddParameter("ClearWeeklyDayCounter", clearWeeklyDayCounter, SqlDbType.Bit)
            .Build();

        return Db.ExecuteAsync(sp, ct);
    }
}
