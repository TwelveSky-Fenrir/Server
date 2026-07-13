using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum GuildInviteAskResultKind
{
    NotAuthorized,
    TargetNotFound,
    TargetAlreadyGuilded,
    TribeMismatch,
    AskerBusy,
    TargetBusy,
    Sent,

    SentCrossShard
}

public interface IGuildInviteService
{
    public ValueTask<GuildInviteAskResultKind> AskAsync(PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);
}
