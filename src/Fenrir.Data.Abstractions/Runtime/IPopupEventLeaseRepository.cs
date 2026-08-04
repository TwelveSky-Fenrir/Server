namespace Fenrir.Data.Abstractions.Runtime;

public interface IPopupEventLeaseRepository
{
    public ValueTask<PopupEventLeaseAcquireResult> TryAcquireAsync(string occurrenceKey, Guid leaseOwnerId,
        short leaseDurationSeconds, CancellationToken ct);
}
