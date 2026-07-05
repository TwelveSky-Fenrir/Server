using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_TEACHER_STATE_SEND query resolved.</summary>
public enum MentorStatusResultKind
{
    NoPartner,
    PartnerNotInZone,
    Resolved
}

public readonly record struct MentorStatusResult(MentorStatusResultKind Kind, int Result = 0);

public interface IMentorStatusService
{
    public MentorStatusResult GetStatus(Zone zone, PlayerRuntimeState state);
}
