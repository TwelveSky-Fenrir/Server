using System.Collections.Immutable;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum ZoneEventRelayOutboxStatus : byte
{
    Pending = 0,
    InFlight = 1,
    Published = 2
}

public static class ZoneEventRelayOutboxLimits
{
    public const int PayloadSize = 130;
    public const int MaximumPendingEntries = 256;
    public const int MaximumClaimCount = 64;
    public const int MinimumLeaseSeconds = 5;
    public const int MaximumLeaseSeconds = 300;
}

public sealed record ZoneEventRelayOutboxEntry(
    byte SourceShardId,
    int Sort,
    byte[] Data,
    Guid OperationId,
    Guid CorrelationId);

public sealed record ZoneEventRelayOutboxClaimRequest(
    byte SourceShardId,
    Guid LeaseId,
    int MaximumCount,
    int LeaseSeconds);

public sealed record ZoneEventRelayOutboxAcknowledgement(
    long OutboxId,
    byte SourceShardId,
    Guid LeaseId);

[GenerateDto]
public sealed partial record ZoneEventRelayOutboxEnqueueResultDto(
    long OutboxId,
    bool IsAccepted,
    bool WasEnqueued);

[GenerateDto]
public sealed partial record ZoneEventRelayOutboxDeliveryDto(
    long OutboxId,
    byte SourceShardId,
    int Sort,
    byte[] Data,
    Guid OperationId,
    Guid CorrelationId,
    int AttemptCount);

[GenerateDto]
public sealed partial record ZoneEventRelayOutboxAcknowledgeResultDto(bool Acknowledged);

public interface IZoneEventRelayOutboxRepository
{
    public ValueTask<ZoneEventRelayOutboxEnqueueResultDto> EnqueueAsync(ZoneEventRelayOutboxEntry entry,
        CancellationToken ct);

    public ValueTask<ImmutableArray<ZoneEventRelayOutboxDeliveryDto>> ClaimAsync(
        ZoneEventRelayOutboxClaimRequest request, CancellationToken ct);

    public ValueTask<bool> AcknowledgeAsync(ZoneEventRelayOutboxAcknowledgement acknowledgement,
        CancellationToken ct);
}
