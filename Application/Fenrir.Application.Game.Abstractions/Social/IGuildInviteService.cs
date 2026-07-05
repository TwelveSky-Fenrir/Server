using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of CZ_GUILD_ASK_SEND's pre-checks, as branched on by <see cref="GuildInviteHandler" />.</summary>
public enum GuildInviteAskResultKind
{
    NotAuthorized, // Abort: asker isn't guild master/sub-master
    TargetNotFound,
    TargetAlreadyGuilded, // Abort
    TribeMismatch, // Abort
    AskerBusy,
    TargetBusy,
    Sent
}

/// <summary>Business logic behind the CZ_GUILD_ASK/ANSWER/CANCEL_SEND opcode family.</summary>
public interface IGuildInviteService
{
    public GuildInviteAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);
}
