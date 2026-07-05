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
    /// </summary>
    public WhisperResolution Resolve(PlayerRuntimeState sender, string targetAvatarName);
}

public enum WhisperOutcome
{
    /// <summary>Sender whispered themselves -- silently ignored, matching the legacy's own posture.</summary>
    SelfWhisper,

    /// <summary>No online player matches <c>targetAvatarName</c> anywhere in the process.</summary>
    TargetNotFound,

    /// <summary>
    ///     Target resolved -- <see cref="WhisperResolution.Target" />/<see cref="WhisperResolution.TargetZone" /> are
    ///     populated.
    /// </summary>
    Delivered
}

public readonly record struct WhisperResolution(
    WhisperOutcome Outcome,
    PlayerRuntimeState? Target,
    Zone? TargetZone);
