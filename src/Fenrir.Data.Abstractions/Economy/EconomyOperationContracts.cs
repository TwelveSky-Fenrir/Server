using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Economy;

public enum EconomyOperationKind : byte
{
    Currency = 1,
    CurrencyAndContainer = 2,
    WarPointAndContainer = 3,
    AccountCashAndContainer = 4,
    NpcService = 5
}

public enum EconomyOperationCause : byte
{
    Npc = 1,
    WarPointShop = 2,
    Reward = 3,
    GameMaster = 4,
    System = 5
}

public enum EconomyOperationStatus : byte
{
    Pending = 0,
    Succeeded = 1,
    Rejected = 2,
    Failed = 3
}

public sealed class EconomyOperationIdempotencyKeyHash
{
    private const int Sha256Length = 32;

    private readonly byte[] value;

    private EconomyOperationIdempotencyKeyHash(byte[] value)
    {
        this.value = value;
    }

    public static EconomyOperationIdempotencyKeyHash FromSha256(ReadOnlySpan<byte> hash)
    {
        if (hash.Length != Sha256Length)
            throw new ArgumentException("An economy operation idempotency-key hash must be 32 bytes.", nameof(hash));

        return new EconomyOperationIdempotencyKeyHash(hash.ToArray());
    }

    public byte[] ToArray()
    {
        return value.ToArray();
    }
}

[GenerateDto]
public sealed partial record EconomyOperationBeginResult(
    Guid OperationId,
    Guid CorrelationId,
    byte Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    bool Begun)
{
    public EconomyOperationStatus OperationStatus => (EconomyOperationStatus)Status;
}

[GenerateDto]
public sealed partial record EconomyOperationCompleteResult(
    Guid OperationId,
    Guid CorrelationId,
    byte Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CompletedNow)
{
    public EconomyOperationStatus OperationStatus => (EconomyOperationStatus)Status;
}
