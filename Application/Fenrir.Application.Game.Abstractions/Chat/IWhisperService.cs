using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_SECRET_CHAT_SEND (opcode 39): self-whisper gate plus process-wide target
///     resolution by avatar name. Cross-tribe gating is commented out in this fork -- inter-tribe whispers
///     pass. No mute gate applies here.
/// </summary>
public interface IWhisperService
{
    /// <summary>
    ///     Resolves the recipient of <paramref name="sender" />'s whisper to <paramref name="targetAvatarName" />.
    ///     Falls back to the cross-shard character-location directory when the same-shard
    ///     <see cref="ZoneRegistry" /> lookup misses -- see <see cref="WhisperOutcome.TargetOnAnotherShard" />
    ///     for why that fallback hit is not the same thing as <see cref="WhisperOutcome.Delivered" />.
    /// </summary>
    public ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        CancellationToken cancellationToken);
}

public enum WhisperOutcome
{
    /// <summary>Sender whispered themselves -- silently ignored, matching the legacy's own posture.</summary>
    SelfWhisper,

    /// <summary>No online player matches <c>targetAvatarName</c> anywhere in the cluster.</summary>
    TargetNotFound,

    /// <summary>
    ///     Target resolved -- <see cref="WhisperResolution.Target" />/<see cref="WhisperResolution.TargetZone" /> are
    ///     populated.
    /// </summary>
    Delivered,

    /// <summary>
    ///     The cross-shard character-location directory found the target alive on a DIFFERENT shard, but this
    ///     codebase has no inter-shard message relay/bus yet -- delivery genuinely cannot happen, which is a
    ///     distinct fact from "nobody online has this name" and must not be silently reported as
    ///     <see cref="TargetNotFound" />. <see cref="WhisperResolution.OtherShardId" />/
    ///     <see cref="WhisperResolution.OtherMapId" /> identify where the target actually is, for logging.
    ///     Actual cross-shard delivery is a follow-up, not implemented here.
    /// </summary>
    TargetOnAnotherShard
}

public readonly record struct WhisperResolution(
    WhisperOutcome Outcome,
    PlayerRuntimeState? Target = null,
    Zone? TargetZone = null,
    byte? OtherShardId = null,
    short? OtherMapId = null);
