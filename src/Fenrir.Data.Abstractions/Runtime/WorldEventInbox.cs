using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record WorldEventInboxReceiptRequest(
    long OutboxId,
    byte DestinationShardId,
    Guid DeliveryLeaseId);

public sealed record WorldEventInboxAcknowledgement(
    long OutboxId,
    byte DestinationShardId,
    Guid DeliveryLeaseId);

public sealed record WorldStateInboundEffectRequest(
    long OutboxId,
    byte DestinationShardId,
    Guid OperationKey,
    byte[] Payload);

public static class WorldStateHighTribeEffectPayload
{
    public const int Size = 3;

    private const byte FormatVersion = 1;
    private const byte SetHighTribeOperation = 1;
    private const byte NoHighTribe = byte.MaxValue;
    private const byte TribeCount = 4;

    public static byte[] Create(byte? highTribe)
    {
        if (highTribe is >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(highTribe));

        return [FormatVersion, SetHighTribeOperation, highTribe ?? NoHighTribe];
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out byte? highTribe)
    {
        highTribe = null;
        if (payload.Length != Size || payload[0] != FormatVersion || payload[1] != SetHighTribeOperation)
            return false;

        if (payload[2] == NoHighTribe)
            return true;
        if (payload[2] >= TribeCount)
            return false;

        highTribe = payload[2];
        return true;
    }
}

[GenerateDto]
public sealed partial record WorldEventInboxReceiptResultDto(long InboxId, bool WasReceived, bool IsEffectCompleted);

[GenerateDto]
public sealed partial record WorldEventInboxAcknowledgeResultDto(bool Acknowledged);

[GenerateDto]
public sealed partial record WorldStateInboundEffectResultDto(bool WasApplied);

public interface IWorldEventInboxRepository
{
    public ValueTask<WorldEventInboxReceiptResultDto> ReceiptAsync(WorldEventInboxReceiptRequest request,
        CancellationToken ct);

    public ValueTask<bool> AcknowledgeAsync(WorldEventInboxAcknowledgement acknowledgement, CancellationToken ct);
}

public interface IWorldEventLocalEffectRepository
{
    public ValueTask<WorldStateInboundEffectResultDto> ApplyWorldStateHighTribeAsync(
        WorldStateInboundEffectRequest request, CancellationToken ct);
}
