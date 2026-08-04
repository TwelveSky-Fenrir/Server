using System.Collections.Immutable;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum WorldEventPayloadCategory : byte
{
    WorldState = 1,
    ZoneWar = 2,
    WorldNotice = 3,
    CrossShardSocial = 4,
    Economy = 5,
    Administration = 6
}

public enum WorldEventOutboxStatus : byte
{
    Pending = 0,
    InFlight = 1,
    Acknowledged = 2,
    DeadLetter = 3
}

public static class WorldEventOutboxLimits
{
    public const int MaximumPayloadBytes = 4096;
    public const int MaximumDeliveryAttempts = 25;
    public const int MaximumReadCount = 256;
    public const int MinimumLeaseSeconds = 5;
    public const int MaximumLeaseSeconds = 300;
}

public sealed record WorldEventOutboxEntry(
    byte SourceShardId,
    long SourceSequence,
    byte DestinationShardId,
    WorldEventPayloadCategory PayloadCategory,
    byte[] Payload,
    Guid CorrelationId,
    Guid IdempotencyKey);

public sealed record WorldEventOutboxReadRequest(
    byte DestinationShardId,
    Guid DeliveryLeaseId,
    int MaximumCount,
    int LeaseSeconds);

[GenerateDto]
public sealed partial record WorldEventOutboxEnqueueResultDto(long OutboxId, bool WasEnqueued);

[GenerateDto]
public sealed partial record WorldEventOutboxDeliveryDto(
    long OutboxId,
    string AuthenticatedSource,
    byte SourceShardId,
    long SourceSequence,
    byte DestinationShardId,
    byte PayloadCategory,
    byte[] Payload,
    byte[] PayloadHash,
    Guid CorrelationId,
    Guid IdempotencyKey,
    short AttemptCount);

public interface IWorldEventOutboxRepository
{
    public ValueTask<WorldEventOutboxEnqueueResultDto> EnqueueAsync(WorldEventOutboxEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<WorldEventOutboxDeliveryDto>> ReadAsync(WorldEventOutboxReadRequest request,
        CancellationToken ct);
}
