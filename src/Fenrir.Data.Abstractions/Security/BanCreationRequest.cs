namespace Fenrir.Data.Abstractions.Security;

public readonly record struct BanCreationRequest(
    int? TargetAccountId,
    int? TargetCharacterId,
    BanReason Reason,
    DateTime? ExpiresAtUtc,
    int ActorAccountId,
    int ActorCharacterId,
    Guid CorrelationId,
    string AuditPayload)
{
    public void EnsureValid()
    {
        if (TargetAccountId is null && TargetCharacterId is null)
            throw new ArgumentException("A ban must identify an account or character target.", nameof(TargetAccountId));

        if (ActorAccountId <= 0 || ActorCharacterId <= 0)
            throw new ArgumentException("A ban requires an authenticated actor account and character.");

        if (CorrelationId == Guid.Empty)
            throw new ArgumentException("A ban requires a non-empty correlation identifier.", nameof(CorrelationId));

        if ((byte)Reason is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(Reason), "A ban reason is outside the supported range.");

        if (string.IsNullOrWhiteSpace(AuditPayload) || AuditPayload.Length > 512)
            throw new ArgumentException("A ban requires an audit payload of at most 512 characters.", nameof(AuditPayload));
    }
}
