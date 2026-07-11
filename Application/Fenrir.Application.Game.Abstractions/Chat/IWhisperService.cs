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
    ///     <see cref="ZoneRegistry" /> lookup misses -- see <see cref="WhisperOutcome.QueuedCrossShard" />
    ///     for why that fallback hit is not the same thing as <see cref="WhisperOutcome.Delivered" />.
    /// </summary>
    public ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        string content, int senderAuthType, CancellationToken cancellationToken);
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
    ///     The same-shard lookup missed but the cross-shard character-location directory found the target alive
    ///     on a DIFFERENT live shard. The whisper has been enqueued onto the cross-shard whisper relay
    ///     (best-effort, fire-and-forget); the sender is acknowledged with Result=0 (accepted, target located)
    ///     carrying <see cref="WhisperResolution.OtherMapId" /> as the target's zone number, exactly as the
    ///     legacy op39 path does the instant its own point-in-time lookup succeeds. Delivery to the target, if
    ///     still present when the relay lands, is a separate best-effort leg with no acknowledgement back to the
    ///     sender (op39 clean-drop parity). <see cref="WhisperResolution.OtherShardId" />/
    ///     <see cref="WhisperResolution.OtherMapId" /> identify where the target was resolved.
    /// </summary>
    QueuedCrossShard
}

public readonly record struct WhisperResolution(
    WhisperOutcome Outcome,
    PlayerRuntimeState? Target = null,
    Zone? TargetZone = null,
    byte? OtherShardId = null,
    short? OtherMapId = null);
