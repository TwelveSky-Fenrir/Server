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
    Sent,

    /// <summary>
    ///     WS1.4 ASK-PUBLISH-ONLY: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS
    ///     resolved on a different live shard via <c>ICharacterShardLocationRepository</c> -- the invite has
    ///     been handed to <c>ISocialCrossShardRelayQueue</c>, but no <c>ISocialCrossShardRelayHandler</c> is
    ///     registered for <c>SocialCrossShardRelayKind.GuildInvite</c> yet, so it is never actually delivered
    ///     today -- see <c>GuildInviteService.AskAsync</c>'s own remarks.
    /// </summary>
    SentCrossShard
}

/// <summary>Business logic behind the CZ_GUILD_ASK/ANSWER/CANCEL_SEND opcode family.</summary>
public interface IGuildInviteService
{
    /// <summary>
    ///     Same-shard lookup first (within <paramref name="zone" />), falling back to the cross-shard
    ///     character-location directory on a miss -- see
    ///     <see cref="GuildInviteAskResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<GuildInviteAskResultKind> AskAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);
}
