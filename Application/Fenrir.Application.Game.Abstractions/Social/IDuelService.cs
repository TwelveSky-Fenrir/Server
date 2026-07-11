using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum DuelAskResultKind
{
    MapForbidden,
    TargetNotFound,
    TribeMismatch,
    ChallengerBusy,
    TargetBusy,
    Sent,

        ChallengerAlreadyDueling
}

public interface IDuelService
{

        public ValueTask<DuelAskResultKind> AskAsync(Zone zone, PlayerRuntimeState challenger, string targetAvatarName,
        int sort, CancellationToken cancellationToken);

        public void Answer(int targetId, int answerCode);

        public void Cancel(int challengerId);

        public void Start(int callerId);
}
