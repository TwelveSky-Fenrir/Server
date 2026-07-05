using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_TEACHER_ASK_SEND attempt resolved.</summary>
public enum MentorAskResultKind
{
    AskerMustDisconnect,
    TargetNotFound,
    TargetMustDisconnect,
    AskerBusy,
    TargetBusy,
    TargetAlreadyHasTeacher,
    TargetAlreadyHasStudent,
    Sent
}

public readonly record struct MentorAskResult(
    MentorAskResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null);

public interface IMentorAskService
{
    public MentorAskResult Ask(Zone zone, PlayerRuntimeState master, string targetAvatarName);
}
