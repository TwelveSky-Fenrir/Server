using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record PopupEventLeaseRepository(ICaeriusNetDbContext Db) : IPopupEventLeaseRepository
{
    public async ValueTask<PopupEventLeaseAcquireResult> TryAcquireAsync(string occurrenceKey, Guid leaseOwnerId,
        short leaseDurationSeconds, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDurationSeconds);

        var sp = new StoredProcedureParametersBuilder("runtime", "usp_PopupEventLease_TryAcquire", 1)
            .AddParameter("OccurrenceKey", occurrenceKey, SqlDbType.VarChar)
            .AddParameter("LeaseOwnerId", leaseOwnerId, SqlDbType.UniqueIdentifier)
            .AddParameter("LeaseDurationSeconds", leaseDurationSeconds, SqlDbType.SmallInt)
            .Build();

        return await Db.FirstQueryAsync<PopupEventLeaseAcquireResult>(sp, ct) ??
               throw new InvalidOperationException(
                   "usp_PopupEventLease_TryAcquire always returns exactly one lease result row.");
    }
}
