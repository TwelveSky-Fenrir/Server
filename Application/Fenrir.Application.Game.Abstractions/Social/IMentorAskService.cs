using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum MentorAskResultKind
{
    AskerMustDisconnect,
    TargetNotFound,
    TargetMustDisconnect,
    AskerBusy,
    TargetBusy,
    TargetAlreadyHasTeacher,
    TargetAlreadyHasStudent,
    Sent,

        SentCrossShard
}

public readonly record struct MentorAskResult(
    MentorAskResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null);

public interface IMentorAskService
{

        public ValueTask<MentorAskResult> AskAsync(Zone zone, PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken);
}
