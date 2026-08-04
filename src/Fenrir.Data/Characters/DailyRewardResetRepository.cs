using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record DailyRewardResetRepository(ICaeriusNetDbContext Db) : IDailyRewardResetRepository
{
    public ValueTask ApplyDailyRewardResetAsync(DateOnly occurrenceDate, bool clearWeeklyDayCounter,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_ResetAll", 0)
            .AddParameter("OccurrenceDate", ToOccurrenceDate(occurrenceDate), SqlDbType.Int)
            .AddParameter("ClearWeeklyDayCounter", clearWeeklyDayCounter, SqlDbType.Bit)
            .Build();

        return Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<DailyRewardResetBroadcastClaimState> TryClaimDailyRewardResetBroadcastAsync(
        DateOnly occurrenceDate, byte shardId, Guid leaseId, int leaseDurationSeconds, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(shardId);
        ArgumentOutOfRangeException.ThrowIfLessThan(leaseDurationSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(leaseDurationSeconds, 300);
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, Guid.Empty);

        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_Reset_TryClaimBroadcast", 1)
            .AddParameter("OccurrenceDate", ToOccurrenceDate(occurrenceDate), SqlDbType.Int)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("LeaseId", leaseId, SqlDbType.UniqueIdentifier)
            .AddParameter("LeaseDurationSeconds", leaseDurationSeconds, SqlDbType.Int)
            .Build();

        var state = (DailyRewardResetBroadcastClaimState)await Db.ExecuteScalarAsync<byte>(sp, ct);
        if (!Enum.IsDefined(state))
            throw new InvalidOperationException($"Unknown daily reward reset broadcast claim state {(byte)state}.");

        return state;
    }

    public async ValueTask<bool> TryCompleteDailyRewardResetBroadcastAsync(DateOnly occurrenceDate, byte shardId,
        Guid leaseId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(shardId);
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, Guid.Empty);

        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountDailyReward_Reset_CompleteBroadcast", 1)
            .AddParameter("OccurrenceDate", ToOccurrenceDate(occurrenceDate), SqlDbType.Int)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("LeaseId", leaseId, SqlDbType.UniqueIdentifier)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    private static int ToOccurrenceDate(DateOnly value) => value.Year * 10_000 + value.Month * 100 + value.Day;
}
