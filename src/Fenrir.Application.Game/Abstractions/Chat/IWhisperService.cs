using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IWhisperService
{
    public ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        string content, int senderAuthType, CancellationToken cancellationToken);
}

public enum WhisperOutcome
{
    SelfWhisper,

    TargetNotFound,

    Delivered,

    QueuedCrossShard
}

public readonly record struct WhisperResolution(
    WhisperOutcome Outcome,
    PlayerRuntimeState? Target = null,
    Zone? TargetZone = null,
    byte? OtherShardId = null,
    short? OtherMapId = null);
