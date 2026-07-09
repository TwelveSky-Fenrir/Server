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
    Sent,

    /// <summary>
    ///     WS1.4 ASK-PUBLISH-ONLY: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS
    ///     resolved on a different live shard via <c>ICharacterShardLocationRepository</c> -- the ask has
    ///     been handed to <c>ISocialCrossShardRelayQueue</c>, but no <c>ISocialCrossShardRelayHandler</c> is
    ///     registered for <c>SocialCrossShardRelayKind.Mentor</c> yet, so it is never actually delivered
    ///     today -- see <c>MentorAskService.AskAsync</c>'s own remarks.
    /// </summary>
    SentCrossShard
}

public readonly record struct MentorAskResult(
    MentorAskResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null);

public interface IMentorAskService
{
    /// <summary>
    ///     Same-shard lookup first (within <paramref name="zone" />), falling back to the cross-shard
    ///     character-location directory on a miss -- see <see cref="MentorAskResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<MentorAskResult> AskAsync(Zone zone, PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken);
}
