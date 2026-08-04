namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct PvpKillCooldownClaim(
    Guid ClaimId,
    short MapId,
    int AttackerCharacterId,
    Guid AttackerIncarnation,
    int DefenderCharacterId,
    Guid DefenderIncarnation,
    bool IsStunTrigger)
{
    public bool IsValid => ClaimId != Guid.Empty &&
                           AttackerCharacterId > 0 &&
                           DefenderCharacterId > 0 &&
                           AttackerCharacterId != DefenderCharacterId &&
                           AttackerIncarnation != Guid.Empty &&
                           DefenderIncarnation != Guid.Empty;
}

public interface IPvpKillCooldownClaimQueue
{
    public bool TryEnqueue(in PvpKillCooldownClaim claim);
}
